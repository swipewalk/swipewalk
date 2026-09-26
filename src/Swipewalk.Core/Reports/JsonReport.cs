using System.Text.Json;
using System.Text.Json.Serialization;

namespace Swipewalk.Core.Reports;

public static class JsonReport
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public static string Serialize(ScanReport report) => JsonSerializer.Serialize(report, Options);

    /// <summary>
    /// Deserializes a results.json. A file saved before <see cref="Model.ScreenResult.ScreenId"/> existed has
    /// no "screenId" property at all, so each affected screen here gets a deterministic id derived from its
    /// position (<c>"legacy-{index}"</c>) instead of that property's own default (a fresh random GUID every
    /// time) -- a random default would give the SAME old file a DIFFERENT id on every separate load (a new
    /// `swipewalk guide` process, a later report open), silently orphaning any guided-check answer saved
    /// against an earlier load. Deterministic-by-position is stable across loads of the same file (record mode
    /// only ever appends screens, never reorders them) even though it can't be a real per-screen identity.
    /// </summary>
    public static ScanReport? Deserialize(string json)
    {
        var report = JsonSerializer.Deserialize<ScanReport>(json, Options);
        if (report is null)
            return report;
        return report with
        {
            Screens = [.. report.Screens.Select((s, i) => string.IsNullOrEmpty(s.ScreenId) ? s with { ScreenId = $"legacy-{i}" } : s)],
        };
    }
}
