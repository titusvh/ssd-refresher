namespace SsdRefresh.Core;

public sealed record ScanSummary(
    long FilesSeen,
    long Hashed,
    long Skipped,
    long Failed,
    long ReadBytes,
    bool WasCanceled);
