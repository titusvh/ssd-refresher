namespace SsdRefresh.Core;

public sealed record ScanProgress(
    long FilesSeen,
    long Hashed,
    long Skipped,
    long Failed,
    long ReadBytes,
    string? CurrentPath,
    DateTimeOffset TimestampUtc);
