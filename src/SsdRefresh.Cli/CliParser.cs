using SsdRefresh.Core;

namespace SsdRefresh.Cli;

internal sealed class CliParser
{
    public ParseResult Parse(string[] args)
    {
        try
        {
            if (args.Length == 0)
            {
                return ParseResult.Invalid("Missing command.");
            }

            return args[0] switch
            {
                "scan" => ParseScan(args.Skip(1).ToArray()),
                "scan-file" => ParseScanFile(args.Skip(1).ToArray()),
                _ => ParseResult.Invalid($"Unknown command: {args[0]}")
            };
        }
        catch (ArgumentException ex)
        {
            return ParseResult.Invalid(ex.Message);
        }
    }

    private static ParseResult ParseScan(string[] args)
    {
        string? path = null;
        string? fileList = null;
        string? manifest = null;
        string? report = null;
        var recursive = false;
        var includeHidden = false;
        var includeSystem = false;
        var includeReparse = false;

        var i = 0;
        if (i < args.Length && !args[i].StartsWith("--"))
        {
            path = args[i++];
        }

        while (i < args.Length)
        {
            switch (args[i])
            {
                case "--file-list": fileList = RequireValue(args, ref i); break;
                case "--manifest": manifest = RequireValue(args, ref i); break;
                case "--report": report = RequireValue(args, ref i); break;
                case "--recursive": recursive = true; i++; break;
                case "--include-hidden": includeHidden = true; i++; break;
                case "--include-system": includeSystem = true; i++; break;
                case "--include-reparse-points": includeReparse = true; i++; break;
                default: return ParseResult.Invalid($"Unknown option: {args[i]}");
            }
        }

        if (string.IsNullOrWhiteSpace(manifest) || string.IsNullOrWhiteSpace(report)) return ParseResult.Invalid("--manifest and --report are required.");
        if (string.IsNullOrWhiteSpace(path) && string.IsNullOrWhiteSpace(fileList)) return ParseResult.Invalid("Provide <path> or --file-list.");

        return ParseResult.Valid(new ScanOptions(path, fileList, manifest, report, recursive, includeHidden, includeSystem, includeReparse));
    }

    private static ParseResult ParseScanFile(string[] args)
    {
        if (args.Length < 5) return ParseResult.Invalid("Usage: scan-file <filePath> --manifest <manifestPath> --report <reportPath>");
        var filePath = args[0];
        return ParseScan([filePath, .. args.Skip(1)]);
    }

    private static string RequireValue(string[] args, ref int i)
    {
        if (i + 1 >= args.Length) throw new ArgumentException($"Missing value for {args[i]}");
        i += 2;
        return args[i - 1];
    }
}

internal sealed record ParseResult(bool IsValid, ScanOptions? Options, string? ErrorMessage)
{
    public static ParseResult Valid(ScanOptions options) => new(true, options, null);
    public static ParseResult Invalid(string error) => new(false, null, error);
}
