using System;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;
using PokerSimPlatform.Core.Engine;
using PokerSimPlatform.Core.Models;
using PokerSimPlatform.Core.Simulation;

namespace PokerSimPlatform.Web.Services;

/// <summary>
/// Erstellt beim ersten Start einen Beispieldatensatz, damit die Matrix-Seite
/// sofort etwas anzeigt ("Sauberes Startbild").
/// </summary>
public sealed class SeedDataService
{
    private readonly ResultStore _store;
    private readonly ILogger<SeedDataService> _logger;

    public SeedDataService(ResultStore store, ILogger<SeedDataService> logger)
    {
        _store = store;
        _logger = logger;
    }

    public void EnsureSeed()
    {
        var existing = _store.LoadAll();
        if (existing.Count > 0)
        {
            _logger.LogInformation("Seed-Übersprungen: {Count} vorhandene Ergebnisse.", existing.Count);
            Console.WriteLine($"[SEED] ⊘ Übersprungen: {existing.Count} vorhandene Ergebnisse.");
            return;
        }

        _logger.LogInformation("Erzeuge Seed-Datensatz…");
        Console.WriteLine("[SEED] ⧖ Erzeuge Seed-Datensatz…");

        // Beispielgegner-Range: Top 20% Starthände (vereinfachte Auswahl)
        var opponentRange = new[]
        {
            "AA","KK","QQ","JJ","TT","99","88","77","66","55","44","33","22",
            "AKs","AQs","AJs","ATs","A9s","KQs","KJs","KTs","QJs","QTs","JTs",
            "AKo","AQo","AJo","ATo","KQo",
        };

        // 6-max Preflop + Flop als Beispieldaten
        var runner = new SimulationRunner(_logger);
        var config6 = TableConfig.SixMax(opponentRange, iterations: 300, samples: 1, flop: false);
        var all169 = StartingHandNotation.All169();

        Console.WriteLine("[SEED] → Phase 1: Preflop");
        var preflop = runner.RunBatch(config6, all169, opponentRange, numOpponents: 1);

        var config6Flop = TableConfig.SixMax(opponentRange, iterations: 300, samples: 1, flop: true);

        Console.WriteLine("[SEED] → Phase 2: Flop");
        var flop = runner.RunBatch(config6Flop, all169, opponentRange, numOpponents: 1);

        _store.Append(preflop.Concat(flop));
        _logger.LogInformation("Seed-Datensatz geschrieben: {Count} Einträge.", preflop.Count + flop.Count);
        Console.WriteLine($"[SEED] ✓ Datensatz geschrieben: {preflop.Count + flop.Count} Einträge.");
    }
}
