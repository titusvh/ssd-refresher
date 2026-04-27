namespace SsdRefresh.Core;

public sealed class ReportRecord
{
    public int SchemaVersion { get; init; } = 1;

    public required string RunId { get; init; }

    public string Operation { get; init; } = "Scan";

    public required string Path { get; init; }

    public ScanStatus Status { get; init; }

    public long? Length { get; init; }

    public string? Sha256 { get; init; }

    public IReadOnlyList<string> Attributes { get; init; } = Array.Empty<string>();

    public string? ExceptionType { get; init; }

    public string? Message { get; init; }

    public long DurationMs { get; init; }

    public DateTimeOffset TimestampUtc { get; init; }
}
