namespace SsdRefresh.Core;

public sealed class ManifestRecord
{
    public int SchemaVersion { get; init; } = 1;

    public required string RunId { get; init; }

    public string Operation { get; init; } = "Scan";

    public required string Path { get; init; }

    public long Length { get; init; }

    public required string Sha256 { get; init; }

    public DateTimeOffset CreationTimeUtc { get; init; }

    public DateTimeOffset LastWriteTimeUtc { get; init; }

    public IReadOnlyList<string> Attributes { get; init; } = Array.Empty<string>();

    public ScanStatus Status { get; init; } = ScanStatus.Hashed;

    public long DurationMs { get; init; }
}
