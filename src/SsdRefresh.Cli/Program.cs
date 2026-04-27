using System.Diagnostics;
using SsdRefresh.Core;

var parser = new CliParser();
var parseResult = parser.Parse(args);
if (!parseResult.Success)
{
    foreach (var error in parseResult.Errors)
    {
        Console.Error.WriteLine(error);
    }

    Console.Error.WriteLine(CliParser.UsageText);
    return 2;
}

var options = new ScanOptions
{
    ManifestPath = parseResult.ManifestPath!,
    ReportPath = parseResult.ReportPath!,
    Recursive = parseResult.Recursive,
    IncludeHidden = parseResult.IncludeHidden,
    IncludeSystem = parseResult.IncludeSystem,
    IncludeReparsePoints = parseResult.IncludeReparsePoints
};

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cts.Cancel();
};

var progressReporter = new ConsoleProgressReporter();
var progress = new Progress<ScanProgress>(progressReporter.Report);
var service = new FileScanService(new FileSelectionService());

try
{
    ScanResult result = parseResult.Mode switch
    {
        ScanMode.ScanPath => await service.ScanPathAsync(parseResult.TargetPath!, options, progress, cts.Token),
        ScanMode.ScanFile => await service.ScanSingleFileAsync(parseResult.TargetPath!, options, progress, cts.Token),
        ScanMode.ScanFileList => await service.ScanFileListAsync(parseResult.FileListPath!, options, progress, cts.Token),
        _ => throw new InvalidOperationException("Unknown scan mode.")
    };

    progressReporter.FlushFinal();
    return result.Summary.Failed > 0 ? 1 : 0;
}
catch (OperationCanceledException)
{
    progressReporter.FlushFinal();
    Console.Error.WriteLine("Scan cancelled.");
    return 130;
}

internal enum ScanMode
{
    ScanPath,
    ScanFile,
    ScanFileList
}

internal sealed class ParseResult
{
    public bool Success => Errors.Count == 0;
    public List<string> Errors { get; } = new();
    public ScanMode Mode { get; set; }
    public string? TargetPath { get; set; }
    public string? FileListPath { get; set; }
    public string? ManifestPath { get; set; }
    public string? ReportPath { get; set; }
    public bool Recursive { get; set; }
    public bool IncludeHidden { get; set; }
    public bool IncludeSystem { get; set; }
    public bool IncludeReparsePoints { get; set; }
}

internal sealed class CliParser
{
    public const string UsageText = """
Usage:
  ssdrefresh scan <path> --manifest <manifestPath> --report <reportPath> [--recursive] [--include-hidden] [--include-system] [--include-reparse-points]
  ssdrefresh scan --file-list <fileListPath> --manifest <manifestPath> --report <reportPath> [--recursive] [--include-hidden] [--include-system] [--include-reparse-points]
  ssdrefresh scan-file <filePath> --manifest <manifestPath> --report <reportPath> [--include-hidden] [--include-system] [--include-reparse-points]
""";

    public ParseResult Parse(string[] args)
    {
        var result = new ParseResult();
        if (args.Length == 0)
        {
            result.Errors.Add("No command provided.");
            return result;
        }

        var command = args[0];
        var optionMap = ParseOptions(args.Skip(1).ToArray(), result.Errors);

        result.ManifestPath = GetOptionValue(optionMap, "--manifest");
        result.ReportPath = GetOptionValue(optionMap, "--report");
        result.Recursive = optionMap.ContainsKey("--recursive");
        result.IncludeHidden = optionMap.ContainsKey("--include-hidden");
        result.IncludeSystem = optionMap.ContainsKey("--include-system");
        result.IncludeReparsePoints = optionMap.ContainsKey("--include-reparse-points");

        if (string.IsNullOrWhiteSpace(result.ManifestPath))
        {
            result.Errors.Add("--manifest is required.");
        }

        if (string.IsNullOrWhiteSpace(result.ReportPath))
        {
            result.Errors.Add("--report is required.");
        }

        var positional = optionMap.TryGetValue("__positional__", out var values) ? values : new List<string>();

        switch (command)
        {
            case "scan":
                var fileListPath = GetOptionValue(optionMap, "--file-list");
                if (!string.IsNullOrWhiteSpace(fileListPath))
                {
                    result.Mode = ScanMode.ScanFileList;
                    result.FileListPath = fileListPath;
                }
                else
                {
                    result.Mode = ScanMode.ScanPath;
                    result.TargetPath = positional.FirstOrDefault();
                    if (string.IsNullOrWhiteSpace(result.TargetPath))
                    {
                        result.Errors.Add("scan requires <path> or --file-list.");
                    }
                }
                break;
            case "scan-file":
                result.Mode = ScanMode.ScanFile;
                result.TargetPath = positional.FirstOrDefault();
                if (string.IsNullOrWhiteSpace(result.TargetPath))
                {
                    result.Errors.Add("scan-file requires <filePath>.");
                }
                break;
            default:
                result.Errors.Add($"Unsupported command: {command}");
                break;
        }

        return result;
    }

    private static Dictionary<string, List<string>> ParseOptions(string[] tokens, List<string> errors)
    {
        var map = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["__positional__"] = new List<string>()
        };

        for (var i = 0; i < tokens.Length; i++)
        {
            var token = tokens[i];
            if (!token.StartsWith("--", StringComparison.Ordinal))
            {
                map["__positional__"].Add(token);
                continue;
            }

            if (token is "--recursive" or "--include-hidden" or "--include-system" or "--include-reparse-points")
            {
                map[token] = new List<string>();
                continue;
            }

            if (i + 1 >= tokens.Length)
            {
                errors.Add($"Missing value for {token}.");
                continue;
            }

            map[token] = new List<string> { tokens[++i] };
        }

        return map;
    }

    private static string? GetOptionValue(Dictionary<string, List<string>> optionMap, string key)
    {
        return optionMap.TryGetValue(key, out var values) ? values.FirstOrDefault() : null;
    }
}

internal sealed class ConsoleProgressReporter
{
    private DateTimeOffset _last = DateTimeOffset.MinValue;
    private readonly Stopwatch _speedWatch = Stopwatch.StartNew();
    private long _lastBytes;
    private ScanProgress? _latest;

    public void Report(ScanProgress progress)
    {
        _latest = progress;
        var now = DateTimeOffset.UtcNow;
        if ((now - _last).TotalSeconds < 1)
        {
            return;
        }

        Print(progress);
        _last = now;
    }

    public void FlushFinal()
    {
        if (_latest is not null)
        {
            Print(_latest);
        }
    }

    private void Print(ScanProgress progress)
    {
        var bytesDelta = progress.ReadBytes - _lastBytes;
        var seconds = Math.Max(1e-6, _speedWatch.Elapsed.TotalSeconds);
        var speed = (bytesDelta / 1024d / 1024d) / seconds;
        _speedWatch.Restart();
        _lastBytes = progress.ReadBytes;
        var current = ShortenPath(progress.CurrentPath ?? string.Empty, 72);

        Console.WriteLine(
            $"Files seen: {progress.FilesSeen} | Hashed: {progress.Hashed} | Skipped: {progress.Skipped} | Failed: {progress.Failed} | Read: {progress.ReadBytes / 1024d / 1024d / 1024d:F1} GB | Speed: {speed:F0} MB/s | Current: {current}");
    }

    private static string ShortenPath(string value, int maxLen)
    {
        if (value.Length <= maxLen)
        {
            return value;
        }

        return value[..Math.Max(0, maxLen - 3)] + "...";
    }
}
