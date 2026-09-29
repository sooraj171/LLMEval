using System.Text.Json;
using System.Text.Json.Serialization;

namespace LLMEval;

/// <summary>One suite-run summary persisted to the optional JSONL history file.</summary>
public sealed class RunHistoryEntry
{
    [JsonPropertyName("timestamp")]
    public DateTimeOffset Timestamp { get; init; }

    [JsonPropertyName("passRate")]
    public double PassRate { get; init; }

    [JsonPropertyName("total")]
    public int Total { get; init; }

    [JsonPropertyName("passed")]
    public int Passed { get; init; }

    [JsonPropertyName("failed")]
    public int Failed { get; init; }

    /// <summary>Average score per metric name across cases in the run.</summary>
    [JsonPropertyName("metricAverages")]
    public Dictionary<string, double>? MetricAverages { get; init; }

    public static RunHistoryEntry From(SuiteRunResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        Dictionary<string, double>? averages = null;
        if (result.Cases is { Count: > 0 })
        {
            averages = result.Cases
                .Where(c => !string.IsNullOrWhiteSpace(c.MetricName))
                .GroupBy(c => c.MetricName!, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    g => g.Key,
                    g => g.Average(c => c.Score),
                    StringComparer.OrdinalIgnoreCase);
            if (averages.Count == 0)
                averages = null;
        }

        var timestamp = result.CompletedAt != default ? result.CompletedAt : DateTimeOffset.UtcNow;
        return new RunHistoryEntry
        {
            Timestamp = timestamp,
            PassRate = result.PassRate,
            Total = result.Total,
            Passed = result.Passed,
            Failed = result.Failed,
            MetricAverages = averages
        };
    }
}

/// <summary>Append-only JSONL store for suite run summaries (opt-in via <see cref="LLMEvalOptions.EnableRunHistory"/>).</summary>
public static class RunHistoryStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static async Task<IReadOnlyList<RunHistoryEntry>> AppendAndReadAsync(
        string path,
        SuiteRunResult result,
        int maxEntries,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(result);

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var entry = RunHistoryEntry.From(result);
        var line = JsonSerializer.Serialize(entry, JsonOptions) + Environment.NewLine;
        await File.AppendAllTextAsync(path, line, cancellationToken).ConfigureAwait(false);

        return await ReadLastAsync(path, maxEntries, cancellationToken).ConfigureAwait(false);
    }

    public static async Task<IReadOnlyList<RunHistoryEntry>> ReadLastAsync(
        string path,
        int maxEntries,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(path) || maxEntries <= 0)
            return Array.Empty<RunHistoryEntry>();

        var lines = await File.ReadAllLinesAsync(path, cancellationToken).ConfigureAwait(false);
        var list = new List<RunHistoryEntry>(Math.Min(lines.Length, maxEntries));
        foreach (var raw in lines)
        {
            if (string.IsNullOrWhiteSpace(raw))
                continue;
            try
            {
                var parsed = JsonSerializer.Deserialize<RunHistoryEntry>(raw, JsonOptions);
                if (parsed != null)
                    list.Add(parsed);
            }
            catch (JsonException)
            {
                // skip corrupt lines
            }
        }

        if (list.Count <= maxEntries)
            return list;
        return list.Skip(list.Count - maxEntries).ToArray();
    }
}
