using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using PokerSimPlatform.Core.Engine;
using PokerSimPlatform.Core.Models;
using PokerSimPlatform.Core.Simulation;

namespace W_Seminar.Pages;

[IgnoreAntiforgeryToken]
public class SimulationModel : PageModel
{
    private readonly ResultStore _store;
    private readonly ILogger<SimulationModel> _logger;

    public SimulationModel(ResultStore store, ILogger<SimulationModel> logger)
    {
        _store = store;
        _logger = logger;
    }

    [BindProperty]
    public SimulationInput Input { get; set; } = new();

    public SimulationOutcome? Result { get; set; }
    public bool IsRunning { get; set; }

    public void OnGet() { }

    public IActionResult OnPost()
    {
        _logger.LogInformation("[UI] OnPost вызван");
        Console.WriteLine("[UI] ⧖ OnPost вызван");

        if (!ModelState.IsValid)
        {
            _logger.LogWarning("[UI] ModelState invalid: {Errors}", 
                string.Join("; ", ModelState.Values.SelectMany(v => v.Errors.Select(e => e.ErrorMessage))));
            Console.WriteLine($"[UI] ! ModelState invalid");
            return Page();
        }

        _logger.LogInformation("[UI] ModelState valid, Input: TableSize={Size}, Phase={Phase}, MC={MC}, NumOpp={NumOpp}",
            Input.TableSize, Input.Phase, Input.MonteCarloIterations, Input.NumOpponents);
        Console.WriteLine($"[UI] ✓ Input valid: {Input.TableSize}-max, {Input.Phase}, {Input.MonteCarloIterations}MC, {Input.NumOpponents} opponents");

        IsRunning = true;

        // Defaults
        List<string> opponentRange = string.IsNullOrWhiteSpace(Input.OpponentRange)
            ? new List<string> { "AA","KK","QQ","JJ","TT","99","88","77","66","55",
                      "AKs","AQs","AJs","ATs","KQs","KJs","QJs",
                      "AKo","AQo","AJo","KQo" }
            : Input.OpponentRange.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();

        var allHands = StartingHandNotation.All169();
        List<string> handFilter = string.IsNullOrWhiteSpace(Input.HandFilter)
            ? allHands.ToList()
            : Input.HandFilter.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();

        // Validiere Filter-Notationen
        foreach (var h in handFilter.Concat(opponentRange))
        {
            try { _ = StartingHandNotation.Expand(h); }
            catch (Exception ex)
            {
                ModelState.AddModelError("Input.HandFilter", $"Ungültige Notation '{h}': {ex.Message}");
                return Page();
            }
        }

        // Validiere Board bei Endgame
        List<Card>? board = null;
        if (Input.Phase == "Endgame")
        {
            if (string.IsNullOrWhiteSpace(Input.Board))
            {
                ModelState.AddModelError("Input.Board", "Board ist erforderlich für Endgame-Phase.");
                return Page();
            }

            try
            {
                board = ParseBoard(Input.Board);
                if (board.Count != 5)
                {
                    ModelState.AddModelError("Input.Board", $"Board muss genau 5 Karten enthalten (gefunden: {board.Count}).");
                    return Page();
                }
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("Input.Board", $"Ungültiges Board-Format: {ex.Message}");
                return Page();
            }
        }

        TableConfig config = Input.TableSize == 9
            ? TableConfig.NineMax(opponentRange, Input.MonteCarloIterations, samples: 1, flop: Input.Phase == "Flop")
            : TableConfig.SixMax(opponentRange, Input.MonteCarloIterations, samples: 1, flop: Input.Phase == "Flop");

        var sw = Stopwatch.StartNew();
        var runner = new SimulationRunner(_logger);

        _logger.LogInformation("[UI] Starte Simulation: {Hands} Hände @ {Size}-max, Phase: {Phase}",
            handFilter.Count, Input.TableSize, Input.Phase);
        Console.WriteLine($"[UI] ⧖ Simulation gestartet: {handFilter.Count} Hände @ {Input.TableSize}-max, {Input.Phase}");

        IReadOnlyList<SimulationResult> results;
        try
        {
            // Board nur bei Endgame übergeben
            results = board != null
                ? runner.RunBatchEndgame(config, handFilter, opponentRange, board, numOpponents: Input.NumOpponents,
                    progress: (cur, tot) => _logger.LogInformation("Sim {Cur}/{Tot}", cur, tot),
                    pot: Input.Pot, callCost: Input.CallCost)
                : runner.RunBatch(config, handFilter, opponentRange, numOpponents: Input.NumOpponents,
                    progress: (cur, tot) => _logger.LogInformation("Sim {Cur}/{Tot}", cur, tot),
                    pot: Input.Pot, callCost: Input.CallCost);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Simulation fehlgeschlagen");
            ModelState.AddModelError("", $"Fehler: {ex.Message}");
            return Page();
        }
        sw.Stop();

        _store.Append(results);

        _logger.LogInformation("[UI] Simulation beendet: {Count} Ergebnisse in {ElapsedMs}ms",
            results.Count, sw.ElapsedMilliseconds);
        Console.WriteLine($"[UI] ✓ Simulation beendet: {results.Count} Ergebnisse in {sw.ElapsedMilliseconds}ms");

        Result = new SimulationOutcome(
            runner.SessionId,
            results.Count,
            sw.Elapsed.TotalSeconds,
            _store.FilePath
        );

        IsRunning = false;
        _logger.LogInformation("Simulation {Session}: {Count} Samples in {Sec:F2}s",
            runner.SessionId, results.Count, sw.Elapsed.TotalSeconds);

        return Page();
    }

    /// <summary>
    /// Parse Board aus Notation (z.B. "AsKhQd2c3h")
    /// </summary>
    private List<Card> ParseBoard(string boardString)
    {
        boardString = boardString.Trim();
        if (boardString.Length % 2 != 0)
            throw new FormatException("Board muss aus Paaren von Zeichen bestehen (Rank + Suit)");

        var cards = new List<Card>();
        for (int i = 0; i < boardString.Length; i += 2)
        {
            string cardStr = boardString.Substring(i, 2);
            try
            {
                var card = Card.Parse(cardStr);
                cards.Add(card);
            }
            catch (Exception ex)
            {
                throw new FormatException($"Ungültige Karte: {cardStr} - {ex.Message}");
            }
        }
        return cards;
    }
}

