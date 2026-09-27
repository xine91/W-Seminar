using System.Collections.Generic;
using System.Linq;
using PokerSimPlatform.Core.Models;

namespace PokerSimPlatform.Core.Simulation;

/// <summary>
/// Aggregationsergebnis für eine einzelne Zelle der 13×13-Matrix.
/// </summary>
public record MatrixCell(
    string Hand,
    int TotalSamples,
    double AvgEquity,
    double RaiseRate,
    double CallRate,
    double FoldRate,
    double AvgExpectedValue = 0
);

/// <summary>
/// Liest Simulationsergebnisse und erzeugt eine 13×13-Matrix aggregiert nach Starthand und Position.
/// </summary>
public static class MatrixAggregator
{
    private static readonly char[] Ranks = { 'A', 'K', 'Q', 'J', 'T', '9', '8', '7', '6', '5', '4', '3', '2' };

    /// <summary>
    /// Liefert die 13 Ränge in Matrix-Reihenfolge (Reihen und Spalten).
    /// </summary>
    public static IReadOnlyList<char> RanksOrder() => Ranks;

    /// <summary>
    /// Erzeugt die Matrix-Notation für eine Zelle: (rowRank, colRank).
    /// - Diagonale (row == col) → Pair (z. B. "AA")
    /// - row &lt; col → suited (z. B. "AKs")
    /// - row &gt; col → offsuit (z. B. "AKo")
    /// </summary>
    public static string CellNotation(char rowRank, char colRank)
    {
        if (rowRank == colRank) return $"{rowRank}{colRank}";
        // Höherer Rang zuerst
        char hi = rowRank, lo = colRank;
        bool suited;
        if (RankValue(hi) > RankValue(lo)) suited = false; // obere Dreiecksmatrix: row = high, col = low → offsuit
        else suited = true;                                  // untere Dreiecksmatrix: row = low, col = high → suited
        // Hmm: konventionell zeigt die obere Hälfte suited, untere offsuit.
        // Wir nutzen: wenn row &lt; col (row früher im Array) → suited
        int rowIdx = System.Array.IndexOf(Ranks, rowRank);
        int colIdx = System.Array.IndexOf(Ranks, colRank);
        if (rowIdx < colIdx) suited = true;
        else if (rowIdx > colIdx) suited = false;
        else return $"{rowRank}{colRank}";
        // Höherer Rang zuerst
        char high = RankValue(rowRank) > RankValue(colRank) ? rowRank : colRank;
        char low = RankValue(rowRank) > RankValue(colRank) ? colRank : rowRank;
        return $"{high}{low}{(suited ? "s" : "o")}";
    }

    private static int RankValue(char c) => c switch
    {
        'A' => 14, 'K' => 13, 'Q' => 12, 'J' => 11, 'T' => 10,
        '9' => 9, '8' => 8, '7' => 7, '6' => 6, '5' => 5, '4' => 4, '3' => 3, '2' => 2,
        _ => 0
    };

    /// <summary>
    /// Erzeugt die 13×13-Matrix für eine bestimmte Position.
    /// </summary>
    public static MatrixCell[,] BuildMatrix(IReadOnlyList<SimulationResult> results, TablePosition? positionFilter)
    {
        var filtered = positionFilter.HasValue
            ? results.Where(r => r.Position == positionFilter.Value).ToList()
            : results.ToList();

        var cells = new MatrixCell[13, 13];
        for (int r = 0; r < 13; r++)
        {
            for (int c = 0; c < 13; c++)
            {
                var notation = CellNotation(Ranks[r], Ranks[c]);
                var matching = filtered.Where(x => x.StartingHand == notation).ToList();
                if (matching.Count == 0)
                {
                    cells[r, c] = new MatrixCell(notation, 0, 0, 0, 0, 0, 0);
                    continue;
                }
                double avgEq = matching.Average(x => x.Equity);
                double avgEW = matching.Average(x => x.ExpectedValue);
                int raises = matching.Count(x => x.Decision == PlayerDecision.Raise);
                int calls = matching.Count(x => x.Decision == PlayerDecision.Call);
                int folds = matching.Count(x => x.Decision == PlayerDecision.Fold);
                int total = matching.Count;
                cells[r, c] = new MatrixCell(
                    notation, total, avgEq,
                    (double)raises / total,
                    (double)calls / total,
                    (double)folds / total,
                    avgEW
                );
            }
        }
        return cells;
    }
}
