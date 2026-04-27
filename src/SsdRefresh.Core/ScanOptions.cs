namespace SsdRefresh.Core;

public sealed record ScanOptions
{
    public required string ManifestPath { get; init; }
    public required string ReportPath { get; init; }
    public bool Recursive { get; init; }
    public bool IncludeHidden { get; init; }
    public bool IncludeSystem { get; init; }
    public bool IncludeReparsePoints { get; init; }
    public int BufferSizeBytes { get; init; } = 1024 * 1024;
}
