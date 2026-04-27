namespace SsdRefresh.Core;

public sealed class ScanTarget
{
    public required string Path { get; init; }
    public ScanStatus? PreScanStatus { get; init; }
    public string[] Attributes { get; init; } = Array.Empty<string>();
    public string? ExceptionType { get; init; }
    public string? Message { get; init; }

    public bool ShouldSkipScan => PreScanStatus.HasValue;
}
