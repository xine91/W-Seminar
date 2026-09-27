using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.Extensions.Logging;
using PokerSimPlatform.Core.Engine;
using PokerSimPlatform.Core.Models;

namespace PokerSimPlatform.Core.Simulation;

/// <summary>
/// Entscheidungs-Policy: Wandelt Equity + Position in Fold/Call/Raise um.
/// Vereinfachte Policy (für Simulationszwecke):
/// - Equity >= 0.55 → Raise
/// - Equity >= 0.30 → Call
/// - sonst → Fold
/// Die Schwellen sind über die Position anpassbar.
/// </summary>
public sealed class DecisionPolicy
{
    public double RaiseThreshold { get; init; } = 0.55;
    public double CallThreshold { get; init; } = 0.30;
    public double EWRaiseThreshold { get; init; } = 0.2; // EW > 0.2 → Raise
    public double EWCallThreshold { get; init; } = -0.1; // EW > -0.1 → Call

    /// <summary>
    /// Positionsabhängige Anpassung: Frühe Positionen (UTG) sind konservativer,
    /// späte (BTN, CO) können mehr Hände spielen.
    /// </summary>
    private static readonly Dictionary<TablePosition, (double RaiseT, double CallT)> PositionThresholds = new()
    {
        { TablePosition.UTG,  (0.65, 0.40) },
        { TablePosition.UTG1, (0.62, 0.38) },
        { TablePosition.MP,   (0.58, 0.35) },
        { TablePosition.MP1,  (0.56, 0.32) },
        { TablePosition.HJ,   (0.54, 0.30) },
        { TablePosition.CO,   (0.50, 0.27) },
        { TablePosition.BTN,  (0.45, 0.24) },
        { TablePosition.SB,   (0.55, 0.32) },
        { TablePosition.BB,   (0.45, 0.20) },
    };

    public PlayerDecision Decide(double equity, TablePosition position)
    {
        var (raiseT, callT) = PositionThresholds.TryGetValue(position, out var t)
            ? t : (RaiseThreshold, CallThreshold);

        if (equity >= raiseT) return PlayerDecision.Raise;
        if (equity >= callT) return PlayerDecision.Call;
        return PlayerDecision.Fold;
    }

    /// <summary>
    /// Entscheidung basierend auf Expected Value statt Equity.
    /// Positive EW → Call/Raise, negative EW → Fold.
    /// </summary>
    public PlayerDecision DecideByExpectedValue(double expectedValue, TablePosition position)
    {
        if (expectedValue >= EWRaiseThreshold) return PlayerDecision.Raise;
        if (expectedValue >= EWCallThreshold) return PlayerDecision.Call;
        return PlayerDecision.Fold;
    }
}

/// <summary>
/// Batch-Simulator: Iteriert über alle (Position × Starthand)-Kombinationen,
/// berechnet Equity, leitet Entscheidung ab und persistiert Ergebnisse.
/// </summary>
public sealed class SimulationRunner
{
    private readonly DecisionPolicy _policy = new();
    private readonly ILogger? _logger;

    public string SessionId { get; } = Guid.NewGuid().ToString("N")[..12];

    public SimulationRunner(ILogger? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Führt eine vollständige Batch-Simulation aus.
    /// </summary>
    /// <param name="config">Tischkonfiguration</param>
    /// <param name="startingHands">Starthände (z. B. alle 169 oder eine Auswahl)</param>
    /// <param name="opponentRange">Gegnerrange (z. B. Top 20% Hände)</param>
    /// <param name="numOpponents">Anzahl konkreter Gegner in der Hand (1 = Heads-Up)</param>
    /// <param name="progress">Callback für Fortschritt (current, total)</param>
    /// <param name="pot">Pot-Größe für EW-Berechnung (optional)</param>
    /// <param name="callCost">Call-Kosten für EW-Berechnung (optional)</param>
    public IReadOnlyList<SimulationResult> RunBatch(
        TableConfig config,
        IReadOnlyList<string> startingHands,
        IReadOnlyList<string> opponentRange,
        int numOpponents = 1,
        Action<int, int>? progress = null,
        double pot = 0,
        double callCost = 0)
    {
        var sw = Stopwatch.StartNew();
        _logger?.LogInformation("[SIM {Session}] Starte Batch: {Positions} Positionen × {Hands} Hände × {MC} MC-Iterationen",
            SessionId, config.Positions.Count, startingHands.Count, config.MonteCarloIterations);
        Console.WriteLine($"[SIM {SessionId}] Starte Batch: {config.Positions.Count} Positionen × {startingHands.Count} Hände × {config.MonteCarloIterations} Iterationen");

        var results = new List<SimulationResult>();
        int total = config.Positions.Count * startingHands.Count;
        int current = 0;
        var equityCalc = new EquityCalculator(config.MonteCarloIterations);

        foreach (var position in config.Positions)
        {
            Console.WriteLine($"  [Position {position}]");
            foreach (var handNotation in startingHands)
            {
                current++;
                progress?.Invoke(current, total);

                // Eine konkrete 2-Karten-Variante der Starthand expandieren
                var expansions = StartingHandNotation.Expand(handNotation);
                if (expansions.Count == 0) continue;
                var (c1, c2) = expansions[0];
                var heroHand = new[] { c1, c2 };

                double equity = numOpponents == 1
                    ? equityCalc.CalculateEquityHeadsUp(heroHand, opponentRange, config.IncludeFlop)
                    : equityCalc.CalculateEquityMultiWay(heroHand, opponentRange, numOpponents, config.IncludeFlop);

                var decision = _policy.Decide(equity, position);

                _logger?.LogDebug("[SIM {Session}] {Hand} @ {Pos}: Eq={Equity:P2} → {Decision}", 
                    SessionId, handNotation, position, equity, decision);

                if (current % 20 == 0)
                    Console.WriteLine($"    Fortschritt: {current:D3}/{total:D3} | {handNotation} @ {position} | Eq: {equity:P2} → {decision}");

                var expectedValue = pot > 0 || callCost > 0
                    ? (pot * equity) - (callCost * (1 - equity))
                    : 0;

                results.Add(new SimulationResult(
                    SessionId: SessionId,
                    Timestamp: DateTime.UtcNow,
                    Position: position,
                    StartingHand: handNotation,
                    Decision: decision,
                    Equity: Math.Round(equity, 6),
                    NumSimulations: config.MonteCarloIterations,
                    TableSize: config.TableSize,
                    NumOpponents: numOpponents,
                    Phase: config.IncludeFlop ? "Flop" : "Preflop",
                    ExpectedValue: Math.Round(expectedValue, 6),
                    Pot: pot,
                    CallCost: callCost
                ));
            }
        }

        sw.Stop();
        _logger?.LogInformation("[SIM {Session}] Batch abgeschlossen: {Count} Ergebnisse in {ElapsedMs}ms",
            SessionId, results.Count, sw.ElapsedMilliseconds);
        Console.WriteLine($"[SIM {SessionId}] ✓ Batch abgeschlossen: {results.Count} Ergebnisse in {sw.ElapsedMilliseconds}ms");

        return results;
    }

    /// <summary>
    /// Batch-Simulation für ENDGAME: Board-Karten sind vollständig bekannt (5 Karten).
    /// Keine Monte-Carlo-Simulation nötig - direkter Vergleich gegen alle Opponent-Hände.
    /// </summary>
    public IReadOnlyList<SimulationResult> RunBatchEndgame(
        TableConfig config,
        IReadOnlyList<string> startingHands,
        IReadOnlyList<string> opponentRange,
        IReadOnlyList<Card> board,
        int numOpponents = 1,
        Action<int, int>? progress = null,
        double pot = 0,
        double callCost = 0)
    {
        if (board.Count != 5)
            throw new ArgumentException("Board muss genau 5 Karten enthalten.", nameof(board));

        var sw = Stopwatch.StartNew();
        _logger?.LogInformation("[SIM {Session}] Starte Endgame-Batch: {Positions} Positionen × {Hands} Hände (deterministisch, kein MC)",
            SessionId, config.Positions.Count, startingHands.Count);
        Console.WriteLine($"[SIM {SessionId}] Starte Endgame-Batch: {config.Positions.Count} Positionen × {startingHands.Count} Hände (deterministisch)");

        var results = new List<SimulationResult>();
        int total = config.Positions.Count * startingHands.Count;
        int current = 0;
        var equityCalc = new EquityCalculator(config.MonteCarloIterations);

        foreach (var position in config.Positions)
        {
            Console.WriteLine($"  [Position {position}]");
            foreach (var handNotation in startingHands)
            {
                current++;
                progress?.Invoke(current, total);

                // Eine konkrete 2-Karten-Variante der Starthand expandieren
                var expansions = StartingHandNotation.Expand(handNotation);
                if (expansions.Count == 0) continue;
                var (c1, c2) = expansions[0];
                var heroHand = new[] { c1, c2 };

                // Endgame: Deterministischer Vergleich (kein MC nötig)
                double equity = numOpponents == 1
                    ? equityCalc.CalculateEquityHeadsUpEndgame(heroHand, opponentRange, board)
                    : equityCalc.CalculateEquityMultiWayEndgame(heroHand, opponentRange, numOpponents, board);

                var decision = _policy.Decide(equity, position);

                _logger?.LogDebug("[SIM {Session}] {Hand} @ {Pos}: Eq={Equity:P2} → {Decision}",
                    SessionId, handNotation, position, equity, decision);

                if (current % 20 == 0)
                    Console.WriteLine($"    Fortschritt: {current:D3}/{total:D3} | {handNotation} @ {position} | Eq: {equity:P2} → {decision}");

                var expectedValue = pot > 0 || callCost > 0
                    ? (pot * equity) - (callCost * (1 - equity))
                    : 0;

                results.Add(new SimulationResult(
                    SessionId: SessionId,
                    Timestamp: DateTime.UtcNow,
                    Position: position,
                    StartingHand: handNotation,
                    Decision: decision,
                    Equity: Math.Round(equity, 6),
                    NumSimulations: config.MonteCarloIterations,
                    TableSize: config.TableSize,
                    NumOpponents: numOpponents,
                    Phase: "Endgame",
                    ExpectedValue: Math.Round(expectedValue, 6),
                    Pot: pot,
                    CallCost: callCost
                ));
            }
        }

        sw.Stop();
        _logger?.LogInformation("[SIM {Session}] Endgame-Batch abgeschlossen: {Count} Ergebnisse in {ElapsedMs}ms",
            SessionId, results.Count, sw.ElapsedMilliseconds);
        Console.WriteLine($"[SIM {SessionId}] ✓ Endgame-Batch abgeschlossen: {results.Count} Ergebnisse in {sw.ElapsedMilliseconds}ms");

        return results;
    }
}
