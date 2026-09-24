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

    public static ScanReport? Deserialize(string json) => JsonSerializer.Deserialize<ScanReport>(json, Options);
}
