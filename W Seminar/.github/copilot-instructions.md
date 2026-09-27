# PokerSim – Quantitative Texas Hold'em Simulation & Auswertung

ASP.NET Core (.NET 10) Razor-Pages-Webplattform zur Monte-Carlo-Equity-Berechnung
in der **Preflop-** und **Flop-Phase** von Texas Hold'em sowie statistischer
Auswertung der Ergebnisse in einer **13×13-Heatmap-Matrix**.

Zielgruppe: analytische / wissenschaftliche Nutzung (z. B. datengetriebene
Poker-Analyse). Fokus auf Korrektheit der Poker-Logik, saubere Datenmodellierung
und übersichtliche Auswertung — nicht auf Spielgrafik.

---

## Features

- **Eigener Poker-Hand-Evaluator** (5-Kart-Enumeration + klassisches 10-Kategorien-Ranking inkl. Wheel-Straight und Royal Flush).
- **Monte-Carlo-Equity-Berechnung** gegen konfigurierbare Gegnerrange, Heads-Up & Multi-Way.
- **169 Starthände** (13 Paare + 78 suited + 78 offsuit) vollständig generiert.
- **Batch-Simulation** über alle 169 Hände × alle aktiven Positionen (6-max oder 9-max).
- **JSON-Persistenz** der Ergebnisse (UTF-8, eingerückt, append-fähig).
- **13×13-Matrix-Visualisierung** als HTML-Tabelle mit CSS-Heatmap (Grün = hohe Equity/Raise, Rot = niedrig/Fold), filterbar nach Position und Phase, Tooltip mit Detailstatistik.
- **Decision-Policy**: positionsabhängige Schwellen (UTG konservativer als BTN).
- **Sauberes Startbild**: Beim ersten Start wird automatisch ein Seed-Datensatz erzeugt, damit die Matrix sofort etwas anzeigt.

## Tech-Stack

- .NET 10 / C# 14
- ASP.NET Core Razor Pages
- xUnit für Tests
- JSON (System.Text.Json) — keine externe Datenbank

## Voraussetzungen

- [.NET 10 SDK](https://dotnet.microsoft.com/download) (getestet mit 10.0.302)
- PowerShell (Windows) oder jedes andere Terminal

## Setup & Start

```powershell
# Aus dem Repository-Root:
dotnet build
dotnet test  # 25 Tests (Hand-Ranking + Equity-Referenzwerte)
dotnet run --project "W Seminar" --urls http://localhost:5000
```

Beim ersten Start wird im Ordner `W Seminar/Data/simulation_results.json`
automatisch ein Seed-Datensatz erzeugt (~2.000 Einträge, 6-max Preflop + Flop).
Diese Datei wird bei weiteren Starts **nicht** überschrieben.

### App im Browser

| URL                          | Zweck                                            |
|------------------------------|--------------------------------------------------|
| `/`                          | Übersicht / Navigation                           |
| `/Simulation`                | Neue Simulation konfigurieren und starten        |
| `/Matrix`                    | 13×13-Heatmap mit Filtern (Position, Phase, Metrik) |
| `/Results`                   | Rohdaten-Tabelle aller Einträge + JSON-Download  |

## Konfiguration

In `W Seminar/appsettings.json` unter `PokerSim`:

| Key                | Default                    | Beschreibung                                      |
|--------------------|----------------------------|---------------------------------------------------|
| `DataDirectory`    | `Data`                     | Verzeichnis für JSON-Ergebnisse (relativ zu ContentRoot) |
| `ResultFileName`   | `simulation_results.json`  | Dateiname                                         |
| `SeedOnStartup`    | `true`                     | Beim ersten Start Seed-Daten erzeugen             |

## Simulationsparameter (UI)

| Parameter              | Default      | Bereich / Hinweis                              |
|------------------------|--------------|------------------------------------------------|
| Tischgröße             | 6            | 6 oder 9                                       |
| Phase                  | Preflop      | Preflop oder Flop (3 Board-Karten)             |
| Gegnerrange            | Top 20 %     | Komma-getrennt, Standard-Poker-Notation        |
| Monte-Carlo-Iterationen | 1000        | 50 – 20 000                                    |
| Anzahl Gegner          | 1            | 1 – 8                                          |
| Starthand-Filter       | alle 169     | optional, z. B. `AA,KK,QQ,AKs`                 |

**Performance-Richtwert**: 169 × 6 × 1000 Iterationen ≈ 1 M Hand-Vergleiche → ca. 5 – 30 s auf Standard-Hardware.

## Poker-Logik (Implementierungsdetails)

- `Core/Engine/Card.cs` — Kartendarstellung (Suit 0–3, Rank 2–14).
- `Core/Engine/Deck.cs` — 52-Karten-Deck, Fisher-Yates Shuffle (mit optionalem Seed für deterministische Tests).
- `Core/Engine/HandRank.cs` — 10 Hand-Ranking-Kategorien.
- `Core/Engine/HandEvaluator.cs` — Bewertung einer 5-Kart-Hand (inkl. Wheel-Straight `A-2-3-4-5`) und `EvaluateBest5Of7` für Texas Hold'em. Klassisches 5-Kart-Enumeration, alle `C(7,5)=21` Kombinationen werden geprüft.
- `Core/Engine/StartingHandNotation.cs` — 169 Starthände, expandierbar zu konkreten Kartentupeln (6 Paare / 4 suited / 12 offsuit pro Notation).
- `Core/Engine/EquityCalculator.cs` — Monte-Carlo-Equity:
  - **Preflop**: 5 zufällige "Showdown-Boards" werden gezogen, dann best-5-card-Hand-Vergleich (Standardpraxis in PokerStove / Equilab).
  - **Flop**: 3 echte Board-Karten, dann best-5-card-Hand-Vergleich.
  - Ties werden halb gezählt.
  - Multi-Way: gleicher Mechanismus, Anteil der Siege über alle Gegner wird gemittelt.

## Datenmodell

```csharp
public record SimulationResult(
	string SessionId,
	DateTime Timestamp,
	TablePosition Position,    // UTG, UTG+1, MP, MP+1, HJ, CO, BTN, SB, BB
	string StartingHand,       // "AKs", "72o", "TT"
	PlayerDecision Decision,   // Fold, Call, Raise
	double Equity,             // 0..1
	int NumSimulations,        // Monte-Carlo-Iterationen
	int TableSize,             // 6 oder 9
	int NumOpponents,
	string Phase               // "Preflop" oder "Flop"
);
```

JSON-Beispiel:

```json
{
  "sessionId": "f259f658436d",
  "timestamp": "2026-07-16T10:26:44.0397376Z",
  "position": 0,
  "startingHand": "AA",
  "decision": 2,
  "equity": 0.858333,
  "numSimulations": 300,
  "tableSize": 6,
  "numOpponents": 1,
  "phase": "Preflop"
}
```

## Tests

```
PokerSimPlatform.Tests/
├── HandEvaluatorTests.cs          # 10-Kategorien-Ranking + Vergleich
├── StartingHandNotationTests.cs   # 169 Hände + Expansion
└── EquityCalculatorTests.cs       # AA vs KK (~80 %), AKo vs 72o (~65 %), Multi-Way-Fall
```

Aktuell **25 Tests, alle grün**.

## Projektstruktur

```
W Seminar/                          # ASP.NET Core Razor Pages Projekt
├── Core/
│   ├── Engine/                     # Card, Deck, HandRank, HandEvaluator,
│   │                                 #   StartingHandNotation, EquityCalculator
│   ├── Models/                     # TablePosition, PlayerDecision,
│   │                                 #   SimulationResult, TableConfig
│   └── Simulation/                 # SimulationRunner, ResultStore, MatrixAggregator
├── Pages/
│   ├── Index.cshtml[.cs]           # Übersicht
│   ├── Simulation.cshtml[.cs]      # Konfiguration + Start
│   ├── Matrix.cshtml[.cs]          # 13×13-Heatmap
│   ├── Results.cshtml[.cs]         # Rohdaten + JSON-Download
│   └── Shared/_Layout.cshtml       # Navigation
├── wwwroot/css/site-poker.css      # Heatmap-Styles
├── Data/simulation_results.json    # persistente Ergebnisse
├── Program.cs                      # DI + Seed-Initialisierung
└── appsettings.json

PokerSimPlatform.Tests/             # xUnit-Testprojekt
```

## Bekannte Einschränkungen / Ausbaumöglichkeiten

- **Turn- und River-Phase**: aktuell nur Preflop (5 Pseudo-Boards) und Flop (3 echte Karten). Turn/River-Erweiterung wäre eine natürliche Folge.
- **Solver-basierte Ranges**: aktuell wird die Gegnerrange als vereinfachte Hand-Liste modelliert. Eine Integration mit echten Solver-Ranges (z. B. aus PioSOLVER-Exporten) wäre möglich.
- **Performance**: bei sehr großen Iterationen (>10 000) und 9-max dauert ein Lauf entsprechend länger. Parallelisierung über `Parallel.For` wäre eine einfache Optimierung.

## Lizenz

Wissenschaftliches / privates Projekt. Eigener Hand-Evaluator ohne externe Lizenzbindungen.
