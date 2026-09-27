using System.Collections.Generic;
using PokerSimPlatform.Core.Engine;

namespace PokerSimPlatform.Core.Models;

/// <summary>
/// Konfiguration für einen Simulations-Lauf.
/// </summary>
public record TableConfig(
    int TableSize,                          // 6 oder 9
    IReadOnlyList<TablePosition> Positions, // aktive Positionen je Tischgröße
    IReadOnlyList<string> OpponentRange,    // Starthand-Range der Gegner
    int MonteCarloIterations = 1000,        // Iterationen pro Equity-Berechnung
    int SamplesPerHand = 10,                // Anzahl simulierter Hände pro Hand×Position
    bool IncludeFlop = false                // Flop-Phase mit 3 Board-Karten
)
{
    public static TableConfig SixMax(IReadOnlyList<string> opponentRange, int iterations = 1000, int samples = 10, bool flop = false) =>
        new(6,
            new[] { TablePosition.UTG, TablePosition.MP, TablePosition.CO, TablePosition.BTN, TablePosition.SB, TablePosition.BB },
            opponentRange, iterations, samples, flop);

    public static TableConfig NineMax(IReadOnlyList<string> opponentRange, int iterations = 1000, int samples = 10, bool flop = false) =>
        new(9,
            new[] { TablePosition.UTG, TablePosition.UTG1, TablePosition.MP, TablePosition.MP1,
                    TablePosition.HJ, TablePosition.CO, TablePosition.BTN, TablePosition.SB, TablePosition.BB },
            opponentRange, iterations, samples, flop);
}

/// <summary>
/// Erweiterte Konfiguration mit Phaseninformation für den Runner.
/// </summary>
public record SimulationConfig(
    TableConfig Table,
    int TableSize,
    int NumOpponents,
    string Phase = "Preflop"
);
