using System;

namespace PokerSimPlatform.Core.Engine;

/// <summary>
/// Repräsentiert eine einzelne Spielkarte.
/// Suit: 0 = Clubs, 1 = Diamonds, 2 = Hearts, 3 = Spades
/// Rank: 2..14 (2..10, 11 = Jack, 12 = Queen, 13 = King, 14 = Ace)
/// </summary>
public readonly record struct Card(int Suit, int Rank)
{
    public static Card Parse(string notation)
    {
        if (string.IsNullOrWhiteSpace(notation) || notation.Length < 2)
            throw new ArgumentException($"Ungültige Kartennotation: '{notation}'", nameof(notation));

        var rankChar = notation[0];
        var suitChar = notation[^1];

        int rank = rankChar switch
        {
            '2' => 2, '3' => 3, '4' => 4, '5' => 5, '6' => 6,
            '7' => 7, '8' => 8, '9' => 9, 'T' => 10, 't' => 10,
            'J' => 11, 'j' => 11, 'Q' => 12, 'q' => 12,
            'K' => 13, 'k' => 13, 'A' => 14, 'a' => 14,
            _ => throw new ArgumentException($"Unbekannter Rang: '{rankChar}'")
        };

        int suit = suitChar switch
        {
            'c' => 0, 'C' => 0,
            'd' => 1, 'D' => 1,
            'h' => 2, 'H' => 2,
            's' => 3, 'S' => 3,
            _ => throw new ArgumentException($"Unbekannte Farbe: '{suitChar}'")
        };

        return new Card(suit, rank);
    }

    public override string ToString()
    {
        string rankStr = Rank switch
        {
            10 => "T",
            11 => "J",
            12 => "Q",
            13 => "K",
            14 => "A",
            _ => Rank.ToString()
        };
        char suitChar = Suit switch
        {
            0 => 'c',
            1 => 'd',
            2 => 'h',
            3 => 's',
            _ => '?'
        };
        return $"{rankStr}{suitChar}";
    }
}
