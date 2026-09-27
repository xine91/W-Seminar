namespace W_Seminar.Pages;

public class SimulationInput
{
    public int TableSize { get; set; } = 6;
    public string Phase { get; set; } = "Preflop";
    public string? OpponentRange { get; set; }
    public int MonteCarloIterations { get; set; } = 1000;
    public int NumOpponents { get; set; } = 1;
    public string? HandFilter { get; set; }
    public string? Board { get; set; }

    // Expected Value Felder
    public double SmallBlind { get; set; } = 0.5;
    public double BigBlind { get; set; } = 1.0;
    public double Pot { get; set; } = 1.5;
    public double CallCost { get; set; } = 1.0;
}

public record SimulationOutcome(string SessionId, int SamplesWritten, double DurationSeconds, string FilePath);
