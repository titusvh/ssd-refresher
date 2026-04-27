using System.Text.Json.Serialization;

namespace SsdRefresh.Core;

public enum ScanStatus
{
    Hashed,
    SkippedUnauthorized,
    SkippedLocked,
    SkippedUnsupportedAttribute,
    SkippedReparsePoint,
    SkippedNotARegularFile,
    SkippedFileNotFound,
    FailedIo,
    FailedUnexpected
}

public sealed record ScanOptions(
    string? Path,
    string? FileListPath,
    string ManifestPath,
    string ReportPath,
    bool Recursive,
    bool IncludeHidden,
    bool IncludeSystem,
    bool IncludeReparsePoints,
    int BufferSizeBytes = 1024 * 1024);

public sealed record ScanProgress(
    long FilesSeen,
    long FilesHashed,
    long FilesSkipped,
    long FilesFailed,
    long ReadBytes,
    string? CurrentPath,
    DateTimeOffset TimestampUtc);

public sealed record ScanSummary(
    long FilesSeen,
    long FilesHashed,
    long FilesSkipped,
    long FilesFailed,
    long ReadBytes)
{
    public bool HasFailures => FilesFailed > 0;
}

public sealed record ScanResult(Guid RunId, ScanSummary Summary, bool IsCancelled);

public sealed record ManifestRecord
{
    public int SchemaVersion { get; init; } = 1;
    public Guid RunId { get; init; }
    public string Operation { get; init; } = "Scan";
    public string Path { get; init; } = string.Empty;
    public long Length { get; init; }
    public string Sha256 { get; init; } = string.Empty;
    public DateTimeOffset CreationTimeUtc { get; init; }
    public DateTimeOffset LastWriteTimeUtc { get; init; }
    public IReadOnlyList<string> Attributes { get; init; } = [];
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ScanStatus Status { get; init; } = ScanStatus.Hashed;
    public long DurationMs { get; init; }
}

public sealed record ReportRecord
{
    public int SchemaVersion { get; init; } = 1;
    public Guid RunId { get; init; }
    public string Operation { get; init; } = "Scan";
    public string Path { get; init; } = string.Empty;
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ScanStatus Status { get; init; }
    public long? Length { get; init; }
    public string? Sha256 { get; init; }
    public IReadOnlyList<string> Attributes { get; init; } = [];
    public string? ExceptionType { get; init; }
    public string? Message { get; init; }
    public long DurationMs { get; init; }
    public DateTimeOffset TimestampUtc { get; init; }
}
