namespace PokerSimPlatform.Core.Models;

/// <summary>
/// Position am Pokertisch. Unterstützt 6-max und 9-max.
/// 6-max: UTG, MP, CO, BTN, SB, BB
/// 9-max: UTG, UTG+1, MP, MP+1, MP+2/HJ, CO, BTN, SB, BB
/// </summary>
public enum TablePosition
{
    UTG,
    UTG1,
    MP,
    MP1,
    HJ,
    CO,
    BTN,
    SB,
    BB
}
