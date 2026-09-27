using System;
using System.Collections.Generic;
using System.Linq;

namespace PokerSimPlatform.Core.Engine;

/// <summary>
/// Bewertet eine 5-Kart-Pokerhand und vergleicht sie mit anderen Händen.
/// 
/// Algorithmus:
/// 1. Bestimme Flush (alle 5 Karten gleiche Farbe).
/// 2. Bestimme Straight (5 aufeinanderfolgende Ränge; A-2-3-4-5 = Wheel-Straight).
/// 3. Bestimme Häufigkeit der Ränge (Count: 4=Quads, 3=Trips, 2=Pair, 1=High).
/// 4. Kombiniere die Kriterien in die 10 HandRank-Kategorien.
/// 5. Vergleiche per Tupel (Category, Kickers) lexikographisch.
/// 
/// Quelle: Standard-Poker-Ranking (5-Card), vergleichbar mit "Hand Evaluator"-Algorithmen
/// wie sie in der Pokerliteratur (Sklansky, etc.) beschrieben sind.
/// </summary>
public static class HandEvaluator
{
    /// <summary>
    /// Bewertet eine 5-Kart-Hand und liefert ein vergleichbares Tupel.
    /// Tupel: (Kategorie, Kickers) — höhere Tupel = stärkere Hand.
    /// </summary>
    public static (HandRank Category, int[] Tiebreakers) Evaluate(Card card1, Card card2, Card card3, Card card4, Card card5)
    {
        var cards = new[] { card1, card2, card3, card4, card5 };
        return Evaluate(cards);
    }

    public static (HandRank Category, int[] Tiebreakers) Evaluate(IReadOnlyList<Card> cards)
    {
        if (cards.Count != 5)
            throw new ArgumentException("Eine 5-Kart-Hand benötigt genau 5 Karten.", nameof(cards));

        // 1. Flush prüfen
        bool isFlush = cards.All(c => c.Suit == cards[0].Suit);

        // 2. Straight prüfen
        var sortedRanks = cards.Select(c => c.Rank).OrderBy(r => r).ToArray();
        bool isStraight;
        bool isWheel = false; // A-2-3-4-5 (niedrigste Straight)
        if (sortedRanks.Distinct().Count() == 5)
        {
            isStraight = sortedRanks[4] - sortedRanks[0] == 4;
            if (!isStraight && sortedRanks[4] == 14 && sortedRanks[0] == 2 && sortedRanks[1] == 3 && sortedRanks[2] == 4 && sortedRanks[3] == 5)
            {
                isStraight = true;
                isWheel = true;
            }
        }
        else
        {
            isStraight = false;
        }

        // 3. Rang-Häufigkeiten bestimmen
        var rankGroups = cards.GroupBy(c => c.Rank)
                              .OrderByDescending(g => g.Count())
                              .ThenByDescending(g => g.Key)
                              .ToList();
        var counts = rankGroups.Select(g => g.Count()).ToArray();
        var rankKeysDesc = rankGroups.Select(g => g.Key).ToArray();

        // 4. Kategorie bestimmen
        HandRank category;
        if (isFlush && isStraight && sortedRanks[0] == 10) // T-J-Q-K-A
            category = HandRank.RoyalFlush;
        else if (isFlush && isStraight)
            category = HandRank.StraightFlush;
        else if (counts[0] == 4)
            category = HandRank.FourOfAKind;
        else if (counts[0] == 3 && counts[1] == 2)
            category = HandRank.FullHouse;
        else if (isFlush)
            category = HandRank.Flush;
        else if (isStraight)
            category = HandRank.Straight;
        else if (counts[0] == 3)
            category = HandRank.ThreeOfAKind;
        else if (counts[0] == 2 && counts[1] == 2)
            category = HandRank.TwoPair;
        else if (counts[0] == 2)
            category = HandRank.OnePair;
        else
            category = HandRank.HighCard;

        // 5. Tiebreaker berechnen
        int[] tiebreakers = BuildTiebreakers(category, rankKeysDesc, isWheel);

        return (category, tiebreakers);
    }

    private static int[] BuildTiebreakers(HandRank category, int[] rankKeysDesc, bool isWheel)
    {
        // Reihenfolge: highest relevant ranks zuerst
        // Bei Wheel-Straight zählt Ass als 1 (niedrigster Straight)
        return category switch
        {
            HandRank.RoyalFlush => new[] { 14 }, // trivial — alle gleich
            HandRank.StraightFlush or HandRank.Straight => isWheel
                ? new[] { 5 } // Wheel: höchste Karte zählt als 5
                : new[] { rankKeysDesc[0] },
            HandRank.FourOfAKind => new[] { rankKeysDesc[0], rankKeysDesc[1] },
            HandRank.FullHouse => new[] { rankKeysDesc[0], rankKeysDesc[1] },
            HandRank.Flush or HandRank.HighCard => rankKeysDesc,
            HandRank.ThreeOfAKind => new[] { rankKeysDesc[0], rankKeysDesc[1], rankKeysDesc[2] },
            HandRank.TwoPair => new[] { rankKeysDesc[0], rankKeysDesc[1], rankKeysDesc[2] },
            HandRank.OnePair => new[] { rankKeysDesc[0], rankKeysDesc[1], rankKeysDesc[2], rankKeysDesc[3] },
            _ => Array.Empty<int>()
        };
    }

    /// <summary>
    /// Vergleicht zwei Hand-Bewertungen. Liefert 1 wenn a > b, -1 wenn a < b, 0 wenn gleich.
    /// </summary>
    public static int Compare((HandRank Category, int[] Tiebreakers) a, (HandRank Category, int[] Tiebreakers) b)
    {
        if (a.Category != b.Category)
            return a.Category > b.Category ? 1 : -1;

        for (int i = 0; i < Math.Min(a.Tiebreakers.Length, b.Tiebreakers.Length); i++)
        {
            if (a.Tiebreakers[i] != b.Tiebreakers[i])
                return a.Tiebreakers[i] > b.Tiebreakers[i] ? 1 : -1;
        }
        return 0;
    }

    /// <summary>
    /// Bewertet die beste 5-Kart-Hand aus 5-7 Karten (Texas Hold'em).
    /// Wählt die Kombination mit der höchsten Bewertung.
    /// </summary>
    public static (HandRank Category, int[] Tiebreakers) EvaluateBest5Of7(IReadOnlyList<Card> cards)
    {
        if (cards.Count < 5)
            throw new ArgumentException("Mindestens 5 Karten erforderlich.", nameof(cards));
        if (cards.Count == 5)
            return Evaluate(cards);

        // Bei 6 oder 7 Karten: alle C(n,5)-Kombinationen durchgehen.
        var indices = Enumerable.Range(0, cards.Count).ToArray();
        var best = ((HandRank)(-1), Array.Empty<int>());
        foreach (var combo in Combinations(indices, 5))
        {
            var hand = combo.Select(i => cards[i]).ToList();
            var eval = Evaluate(hand);
            if (best.Item1 == (HandRank)(-1) || Compare(eval, best) > 0)
                best = eval;
        }
        return best;
    }

    /// <summary>
    /// Generiert alle k-elementigen Kombinationen aus den gegebenen Werten.
    /// </summary>
    private static IEnumerable<T[]> Combinations<T>(T[] source, int k)
    {
        var result = new T[k];
        var indices = new int[k];
        for (int i = 0; i < k; i++) { indices[i] = i; result[i] = source[i]; }
        yield return (T[])result.Clone();

        while (true)
        {
            int i = k - 1;
            while (i >= 0 && indices[i] == source.Length - k + i) i--;
            if (i < 0) yield break;
            indices[i]++;
            for (int j = i + 1; j < k; j++) indices[j] = indices[j - 1] + 1;
            for (int j = 0; j < k; j++) result[j] = source[indices[j]];
            yield return (T[])result.Clone();
        }
    }
}
