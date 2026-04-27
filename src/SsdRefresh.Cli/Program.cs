using System.Diagnostics;
using SsdRefresh.Cli;
using SsdRefresh.Core;

var parser = new CliParser();
var parse = parser.Parse(args);
if (!parse.IsValid || parse.Options is null)
{
    Console.Error.WriteLine(parse.ErrorMessage ?? "Invalid arguments.");
    return 2;
}

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

var service = new FileScanService();
var progressPrinter = new ProgressPrinter();
var progress = new Progress<ScanProgress>(progressPrinter.Report);

try
{
    var result = await service.ScanAsync(parse.Options, progress, cts.Token);
    progressPrinter.Flush();
    return result.Summary.HasFailures ? 1 : 0;
}
catch (OperationCanceledException)
{
    progressPrinter.Flush();
    return 130;
}

internal sealed class ProgressPrinter
{
    private DateTimeOffset _lastPrinted = DateTimeOffset.MinValue;
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
    private ScanProgress? _last;

    public void Report(ScanProgress progress)
    {
        _last = progress;
        if (DateTimeOffset.UtcNow - _lastPrinted < TimeSpan.FromSeconds(1))
        {
            return;
        }

        Print(progress);
        _lastPrinted = DateTimeOffset.UtcNow;
    }

    public void Flush()
    {
        if (_last is not null)
        {
            Print(_last);
        }
    }

    private void Print(ScanProgress progress)
    {
        var mbps = _stopwatch.Elapsed.TotalSeconds <= 0
            ? 0
            : (progress.ReadBytes / 1024d / 1024d) / _stopwatch.Elapsed.TotalSeconds;
        var readGb = progress.ReadBytes / 1024d / 1024d / 1024d;
        var current = progress.CurrentPath is null ? string.Empty : Truncate(progress.CurrentPath, 60);
        Console.WriteLine($"Files seen: {progress.FilesSeen} | Hashed: {progress.FilesHashed} | Skipped: {progress.FilesSkipped} | Failed: {progress.FilesFailed} | Read: {readGb:F1} GB | Speed: {mbps:F0} MB/s | Current: {current}");
    }

    private static string Truncate(string value, int maxLength)
    {
        return value.Length <= maxLength ? value : $"...{value[^Math.Min(maxLength - 3, value.Length)..]}";
    }
}
