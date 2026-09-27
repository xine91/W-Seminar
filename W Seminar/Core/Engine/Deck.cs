using System;
using System.Collections.Generic;

namespace PokerSimPlatform.Core.Engine;

/// <summary>
/// 52-Karten-Deck mit Fisher-Yates Shuffle.
/// Karten werden bei Bedarf gezogen (kein Zurücklegen innerhalb einer Hand).
/// </summary>
public sealed class Deck
{
    private readonly List<Card> _cards;
    private int _nextIndex;

    public Deck(int? seed = null)
    {
        _cards = new List<Card>(52);
        for (int suit = 0; suit < 4; suit++)
        {
            for (int rank = 2; rank <= 14; rank++)
            {
                _cards.Add(new Card(suit, rank));
            }
        }
        Shuffle(seed);
    }

    /// <summary>
    /// Fisher-Yates Shuffle mit optionalem Seed (für deterministische Tests).
    /// </summary>
    public void Shuffle(int? seed = null)
    {
        var rng = seed.HasValue ? new Random(seed.Value) : new Random();
        for (int i = _cards.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (_cards[i], _cards[j]) = (_cards[j], _cards[i]);
        }
        _nextIndex = 0;
    }

    public Card Draw()
    {
        if (_nextIndex >= _cards.Count)
            throw new InvalidOperationException("Deck ist leer.");
        return _cards[_nextIndex++];
    }

    public IReadOnlyList<Card> RemainingCards => _cards.GetRange(_nextIndex, _cards.Count - _nextIndex);

    /// <summary>
    /// Entfernt bestimmte Karten aus dem Deck (z. B. wenn Spieler-Handkarten bereits bekannt sind).
    /// </summary>
    public void RemoveCards(IEnumerable<Card> cardsToRemove)
    {
        var removeSet = new HashSet<Card>(cardsToRemove);
        _cards.RemoveAll(c => removeSet.Contains(c));
        _nextIndex = 0;
    }

    public void Reset() => _nextIndex = 0;
}
