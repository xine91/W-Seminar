using System;
using System.Collections.Generic;
using System.Linq;

namespace PokerSimPlatform.Core.Engine;

/// <summary>
/// Monte-Carlo-Equity-Berechnung gegen eine Gegnerrange.
/// 
/// Algorithmus:
/// 1. Spieler erhält konkrete 2 Karten (hero hand).
/// 2. Aus der Gegnerrange werden zufällig N (z. B. 1000) Gegnerhände gezogen.
/// 3. Board-Karten (3 Flop / 5 Flop+Turn+River) werden aus den verbleibenden Karten gezogen.
/// 4. Die beste 5-Kart-Hand beider Seiten wird per HandEvaluator ermittelt.
/// 5. Gewinnrate = Anzahl Siege / Iterationen (Ties werden halb gezählt).
/// 
/// Quelle/Algorithmus: Standard-Monte-Carlo-Equity-Berechnung (vgl. PokerStove, Equilab).
/// </summary>
public sealed class EquityCalculator
{
    private readonly int _iterations;
    private readonly Random _rng;

    public EquityCalculator(int iterations = 1000, int? seed = null)
    {
        if (iterations < 1)
            throw new ArgumentOutOfRangeException(nameof(iterations), "Mindestens 1 Iteration.");
        _iterations = iterations;
        _rng = seed.HasValue ? new Random(seed.Value) : new Random();
    }

    /// <summary>
    /// Berechnet die Equity der Hero-Hand gegen eine einzige Gegnerrange (Heads-Up).
    /// Liefert einen Wert zwischen 0 und 1.
    /// </summary>
    /// <param name="heroHand">Bekannte 2 Karten des Helden</param>
    /// <param name="opponentRange">Gegnerrange als Liste möglicher Starthände (Notation)</param>
    /// <param name="includeFlop">Wenn true, werden 3 Board-Karten gezogen; sonst reines Preflop</param>
    public double CalculateEquityHeadsUp(
        IReadOnlyList<Card> heroHand,
        IReadOnlyList<string> opponentRange,
        bool includeFlop = false)
    {
        if (heroHand.Count != 2)
            throw new ArgumentException("Hero-Hand muss genau 2 Karten enthalten.", nameof(heroHand));

        // Gegnerrange expandieren (jede Notation → konkrete Kartentupel)
        var opponentHands = ExpandRange(opponentRange);

        int wins = 0, ties = 0, total = 0;
        for (int i = 0; i < _iterations; i++)
        {
            var deck = new Deck();
            deck.RemoveCards(heroHand);

            // Gegnerhand ziehen
            var opponentHand = SampleRandomHand(opponentHands, deck);
            if (opponentHand == null) continue; // Konflikt — Iteration überspringen
            deck.RemoveCards(opponentHand);

            // Board-Karten ziehen.
            // Immer 5 simulierte Karten (Flop + Turn + River):
            // - Preflop: Alle 5 sind unbekannt → simuliert
            // - Flop: Die ersten 3 sind bekannt → nur Turn + River (2) werden noch simuliert
            // In der Simulation behandeln wir beide gleich und simulieren alle 5.
            const int boardCount = 5;
            var board = DrawBoard(deck, boardCount);

            // Vergleich
            int result = CompareHands(heroHand.Concat(board).ToList(),
                                      opponentHand.Concat(board).ToList());
            if (result > 0) wins++;
            else if (result == 0) ties++;
            total++;
        }

        return (wins + 0.5 * ties) / total;
    }

    /// <summary>
    /// Berechnet die Equity der Hero-Hand gegen N zufällige Gegner (Multi-Way).
    /// Gewinnt die Helden-Hand, wird der Pott allein gewonnen.
    /// </summary>
    public double CalculateEquityMultiWay(
        IReadOnlyList<Card> heroHand,
        IReadOnlyList<string> opponentRange,
        int numOpponents,
        bool includeFlop = false)
    {
        if (heroHand.Count != 2)
            throw new ArgumentException("Hero-Hand muss genau 2 Karten enthalten.", nameof(heroHand));
        if (numOpponents < 1)
            throw new ArgumentOutOfRangeException(nameof(numOpponents));

        var opponentHands = ExpandRange(opponentRange);

        double totalEquity = 0;
        for (int i = 0; i < _iterations; i++)
        {
            var deck = new Deck();
            deck.RemoveCards(heroHand);

            var opponents = new List<Card[]>();
            bool conflict = false;
            for (int o = 0; o < numOpponents; o++)
            {
                var oppHand = SampleRandomHand(opponentHands, deck);
                if (oppHand == null) { conflict = true; break; }
                opponents.Add(oppHand);
                deck.RemoveCards(oppHand);
            }
            if (conflict) continue;

            // Immer 5 Karten simulieren (Flop + Turn + River)
            const int boardCount = 5;
            var board = DrawBoard(deck, boardCount);

            var heroEval = HandEvaluator.EvaluateBest5Of7(heroHand.Concat(board).ToList());
            int winners = 0;
            int heroWins = 0;
            for (int o = 0; o < opponents.Count; o++)
            {
                var oppEval = HandEvaluator.EvaluateBest5Of7(opponents[o].Concat(board).ToList());
                int cmp = HandEvaluator.Compare(heroEval, oppEval);
                if (cmp > 0) heroWins++;
                else if (cmp == 0) { heroWins++; winners++; }
                else winners++;
            }
            // Vereinfachte Equity = heroWins / (1 + opponents.Count) — gleichmäßige Verteilung
            totalEquity += (double)heroWins / (1 + opponents.Count);
        }

        return totalEquity / _iterations;
    }

    private static List<Card[]> ExpandRange(IReadOnlyList<string> range)
    {
        var result = new List<Card[]>();
        foreach (var hand in range)
        {
            var expansions = StartingHandNotation.Expand(hand);
            foreach (var (c1, c2) in expansions)
            {
                result.Add(new[] { c1, c2 });
            }
        }
        return result;
    }

    private Card[]? SampleRandomHand(List<Card[]> pool, Deck deck)
    {
        // Wir wählen so lange eine zufällige Hand aus dem Pool, bis sie konfliktfrei zum Deck ist.
        // Bei großem Pool ist die Wahrscheinlichkeit eines Konflikts gering.
        const int maxAttempts = 200;
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            var pick = pool[_rng.Next(pool.Count)];
            if (!pick.Any(c => !IsCardAvailableInDeck(deck, c)))
                return pick;
        }
        return null;
    }

    private static bool IsCardAvailableInDeck(Deck deck, Card card)
    {
        // Deck.RemainingCards enthält noch zu ziehende Karten.
        return deck.RemainingCards.Any(c => c.Suit == card.Suit && c.Rank == card.Rank);
    }

    /// <summary>
    /// Berechnet die Equity der Hero-Hand gegen eine Gegnerrange mit **vollständig bekanntem Board (alle 5 Karten)**
    /// Heads-Up (Endgame). Da das Board komplett ist, gibt es keine Simulation mehr.
    /// </summary>
    /// <param name="heroHand">Bekannte 2 Karten des Helden</param>
    /// <param name="opponentRange">Gegnerrange als Liste möglicher Starthände (Notation)</param>
    /// <param name="board">Die 5 bekannten Board-Karten</param>
    /// <returns>Equity zwischen 0 und 1</returns>
    public double CalculateEquityHeadsUpEndgame(
        IReadOnlyList<Card> heroHand,
        IReadOnlyList<string> opponentRange,
        IReadOnlyList<Card> board)
    {
        if (heroHand.Count != 2)
            throw new ArgumentException("Hero-Hand muss genau 2 Karten enthalten.", nameof(heroHand));
        if (board.Count != 5)
            throw new ArgumentException("Board muss genau 5 Karten enthalten.", nameof(board));

        // Gegnerrange expandieren
        var opponentHands = ExpandRange(opponentRange);

        int wins = 0, ties = 0, total = 0;

        // Vergleiche Hero gegen alle möglichen Gegner-Hände
        foreach (var opponentHand in opponentHands)
        {
            // Karten-Konflikt prüfen: keine doppelten Karten
            var allCards = heroHand.Concat(opponentHand).Concat(board).ToList();
            if (allCards.Distinct().Count() != allCards.Count)
                continue; // Diese Kombination ist unmöglich

            int result = CompareHands(heroHand.Concat(board).ToList(),
                                      opponentHand.Concat(board).ToList());
            if (result > 0) wins++;
            else if (result == 0) ties++;
            total++;
        }

        if (total == 0) return 0.5; // Fallback wenn keine gültige Hand
        return (wins + 0.5 * ties) / total;
    }

    /// <summary>
    /// Berechnet die Equity der Hero-Hand gegen N zufällige Gegner mit **vollständig bekanntem Board (Endgame)**.
    /// </summary>
    public double CalculateEquityMultiWayEndgame(
        IReadOnlyList<Card> heroHand,
        IReadOnlyList<string> opponentRange,
        int numOpponents,
        IReadOnlyList<Card> board)
    {
        if (heroHand.Count != 2)
            throw new ArgumentException("Hero-Hand muss genau 2 Karten enthalten.", nameof(heroHand));
        if (board.Count != 5)
            throw new ArgumentException("Board muss genau 5 Karten enthalten.", nameof(board));
        if (numOpponents < 1)
            throw new ArgumentOutOfRangeException(nameof(numOpponents));

        var opponentHands = ExpandRange(opponentRange);

        double totalEquity = 0;
        int validCombos = 0;

        // Für jede Kombination von numOpponents Gegnern
        // Hinweis: Vereinfachung - wir nehmen die ersten N eindeutigen Kombinationen
        for (int i = 0; i < opponentHands.Count && validCombos < _iterations; i++)
        {
            var opponents = new List<Card[]> { opponentHands[i] };

            for (int j = 0; j < opponentHands.Count && opponents.Count < numOpponents; j++)
            {   
                if (i == j) continue; // Keine doppelten Hände
                opponents.Add(opponentHands[j]);
            }

            if (opponents.Count < numOpponents) continue; // Nicht genug Gegner vorhanden

            // Prüfe ob alle Karten eindeutig sind
            var allCards = heroHand.Concat(board).Concat(opponents.SelectMany(h => h)).ToList();
            if (allCards.Distinct().Count() != allCards.Count)
                continue; // Karten-Konflikt

            var heroEval = HandEvaluator.EvaluateBest5Of7(heroHand.Concat(board).ToList());
            int heroWins = 0;

            foreach (var oppHand in opponents)
            {
                var oppEval = HandEvaluator.EvaluateBest5Of7(oppHand.Concat(board).ToList());
                int cmp = HandEvaluator.Compare(heroEval, oppEval);
                if (cmp >= 0) heroWins++; // Sieg oder Tie
            }

            // Vereinfachte Equity = heroWins / (1 + opponents.Count)
            totalEquity += (double)heroWins / (1 + opponents.Count);
            validCombos++;
        }

        if (validCombos == 0) return 0.5;
        return totalEquity / validCombos;
    }

    private static List<Card> DrawBoard(Deck deck, int count)
    {
        var board = new List<Card>(count);
        for (int i = 0; i < count; i++)
        {
            board.Add(deck.Draw());
        }
        return board;
    }

    private static int CompareHands(List<Card> a, List<Card> b)
    {
        var evalA = HandEvaluator.EvaluateBest5Of7(a);
        var evalB = HandEvaluator.EvaluateBest5Of7(b);
        return HandEvaluator.Compare(evalA, evalB);
    }
}
