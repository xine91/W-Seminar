using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using PokerSimPlatform.Core.Models;

namespace PokerSimPlatform.Core.Simulation;

/// <summary>
/// Persistiert Simulationsergebnisse als JSON-Datei (UTF-8, eingerückt).
/// Liest vorhandene Ergebnisse und schreibt neue append-style.
/// </summary>
public sealed class ResultStore
{
    private readonly string _filePath;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public ResultStore(string filePath)
    {
        _filePath = filePath;
        var dir = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);
    }

    public string FilePath => _filePath;

    public IReadOnlyList<SimulationResult> LoadAll()
    {
        if (!File.Exists(_filePath))
            return Array.Empty<SimulationResult>();

        try
        {
            using var stream = File.OpenRead(_filePath);
            var data = JsonSerializer.Deserialize<List<SimulationResult>>(stream, JsonOptions);
            return data ?? new List<SimulationResult>();
        }
        catch (JsonException)
        {
            // Beschädigte Datei → leere Liste
            return new List<SimulationResult>();
        }
    }

    public async Task AppendAsync(IEnumerable<SimulationResult> newResults, CancellationToken ct = default)
    {
        var existing = LoadAll().ToList();
        existing.AddRange(newResults);
        await using var stream = File.Create(_filePath);
        await JsonSerializer.SerializeAsync(stream, existing, JsonOptions, ct);
    }

    public void Append(IEnumerable<SimulationResult> newResults)
    {
        var existing = LoadAll().ToList();
        existing.AddRange(newResults);
        File.WriteAllText(_filePath, JsonSerializer.Serialize(existing, JsonOptions));
    }

    /// <summary>
    /// Schreibt nur die übergebenen Ergebnisse (überschreibt Datei).
    /// </summary>
    public void Save(IEnumerable<SimulationResult> results)
    {
        File.WriteAllText(_filePath, JsonSerializer.Serialize(results.ToList(), JsonOptions));
    }
}
