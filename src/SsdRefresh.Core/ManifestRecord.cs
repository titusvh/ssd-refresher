namespace SsdRefresh.Core;

public sealed class ManifestRecord
{
    public int SchemaVersion { get; init; } = 1;
    public required string RunId { get; init; }
    public string Operation { get; init; } = "Scan";
    public required string Path { get; init; }
    public required long Length { get; init; }
    public required string Sha256 { get; init; }
    public required DateTimeOffset CreationTimeUtc { get; init; }
    public required DateTimeOffset LastWriteTimeUtc { get; init; }
    public required string[] Attributes { get; init; }
    public ScanStatus Status { get; init; } = ScanStatus.Hashed;
    public long DurationMs { get; set; }
}
