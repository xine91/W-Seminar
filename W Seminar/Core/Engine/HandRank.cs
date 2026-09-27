using System;

namespace PokerSimPlatform.Core.Engine;

/// <summary>
/// Hand-Ranking-Kategorien in absteigender Reihenfolge (höher = besser).
/// Quelle/Algorithmus: Klassisches 5-Kart-Poker-Ranking (Standardregeln).
/// </summary>
public enum HandRank
{
    HighCard = 0,
    OnePair = 1,
    TwoPair = 2,
    ThreeOfAKind = 3,
    Straight = 4,
    Flush = 5,
    FullHouse = 6,
    FourOfAKind = 7,
    StraightFlush = 8,
    RoyalFlush = 9
}
