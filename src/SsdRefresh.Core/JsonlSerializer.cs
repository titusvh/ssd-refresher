using System.Text.Json;

namespace SsdRefresh.Core;

public static class JsonlSerializer
{
    public static string SerializeManifest(ManifestRecord record)
        => JsonSerializer.Serialize(record, JsonlSerializerOptions.Instance);

    public static string SerializeReport(ReportRecord record)
        => JsonSerializer.Serialize(record, JsonlSerializerOptions.Instance);

    public static ManifestRecord DeserializeManifest(string jsonLine)
        => JsonSerializer.Deserialize<ManifestRecord>(jsonLine, JsonlSerializerOptions.Instance)
           ?? throw new JsonException("Unable to deserialize manifest record.");

    public static ReportRecord DeserializeReport(string jsonLine)
        => JsonSerializer.Deserialize<ReportRecord>(jsonLine, JsonlSerializerOptions.Instance)
           ?? throw new JsonException("Unable to deserialize report record.");
}
