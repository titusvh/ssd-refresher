using System.Diagnostics;
using System.Security.Cryptography;

namespace SsdRefresh.Core;

public sealed class FileScanService
{
    private readonly FileSelectionService _fileSelectionService;

    public FileScanService(FileSelectionService fileSelectionService)
    {
        _fileSelectionService = fileSelectionService;
    }

    public Task<ScanResult> ScanPathAsync(string path, ScanOptions options, IProgress<ScanProgress>? progress, CancellationToken cancellationToken)
    {
        return ScanCoreAsync(_fileSelectionService.SelectFromPath(path, options), options, progress, cancellationToken);
    }

    public Task<ScanResult> ScanFileListAsync(string fileListPath, ScanOptions options, IProgress<ScanProgress>? progress, CancellationToken cancellationToken)
    {
        return ScanCoreAsync(_fileSelectionService.SelectFromFileList(fileListPath, options), options, progress, cancellationToken);
    }

    public Task<ScanResult> ScanSingleFileAsync(string filePath, ScanOptions options, IProgress<ScanProgress>? progress, CancellationToken cancellationToken)
    {
        return ScanCoreAsync(new[] { System.IO.Path.GetFullPath(filePath) }, options, progress, cancellationToken);
    }

    private async Task<ScanResult> ScanCoreAsync(IEnumerable<string> filePaths, ScanOptions options, IProgress<ScanProgress>? progress, CancellationToken cancellationToken)
    {
        var runId = Guid.NewGuid().ToString("N");
        var filesSeen = 0L;
        var hashed = 0L;
        var skipped = 0L;
        var failed = 0L;
        var readBytes = 0L;

        await using var manifestWriter = new JsonLinesManifestWriter(options.ManifestPath);
        await using var reportWriter = new JsonLinesReportWriter(options.ReportPath);

        foreach (var path in filePaths.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();
            filesSeen++;
            var watch = Stopwatch.StartNew();
            var timestamp = DateTimeOffset.UtcNow;
            var fullPath = System.IO.Path.GetFullPath(path);

            var result = await ScanOneFileAsync(fullPath, runId, options, cancellationToken).ConfigureAwait(false);
            watch.Stop();

            if (result.ManifestRecord is not null)
            {
                var manifest = result.ManifestRecord;
                manifest.DurationMs = watch.ElapsedMilliseconds;
                await manifestWriter.WriteAsync(manifest, cancellationToken).ConfigureAwait(false);
                hashed++;
            }
            else if (result.Status.ToString().StartsWith("Skipped", StringComparison.Ordinal))
            {
                skipped++;
            }
            else
            {
                failed++;
            }

            readBytes += result.ReadBytes;

            var report = new ReportRecord
            {
                RunId = runId,
                Path = fullPath,
                Status = result.Status,
                Length = result.Length,
                Sha256 = result.Sha256,
                Attributes = result.Attributes,
                ExceptionType = result.ExceptionType,
                Message = result.Message,
                DurationMs = watch.ElapsedMilliseconds,
                TimestampUtc = timestamp
            };

            await reportWriter.WriteAsync(report, cancellationToken).ConfigureAwait(false);

            progress?.Report(new ScanProgress(filesSeen, hashed, skipped, failed, readBytes, fullPath, DateTimeOffset.UtcNow));
        }

        return new ScanResult(new ScanSummary(filesSeen, hashed, skipped, failed, readBytes, false));
    }

    private static async Task<ScanOneResult> ScanOneFileAsync(string fullPath, string runId, ScanOptions options, CancellationToken cancellationToken)
    {
        if (!File.Exists(fullPath))
        {
            return ScanOneResult.Skipped(ScanStatus.SkippedFileNotFound, Array.Empty<string>(), message: "File not found.");
        }

        try
        {
            var attributes = File.GetAttributes(fullPath);
            var attributeNames = GetAttributeNames(attributes);

            if (!options.IncludeReparsePoints && attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                return ScanOneResult.Skipped(ScanStatus.SkippedReparsePoint, attributeNames, message: "Reparse point skipped by default.");
            }

            if (!options.IncludeHidden && attributes.HasFlag(FileAttributes.Hidden))
            {
                return ScanOneResult.Skipped(ScanStatus.SkippedUnsupportedAttribute, attributeNames, message: "Hidden file skipped by default.");
            }

            if (!options.IncludeSystem && attributes.HasFlag(FileAttributes.System))
            {
                return ScanOneResult.Skipped(ScanStatus.SkippedUnsupportedAttribute, attributeNames, message: "System file skipped by default.");
            }

            if (Directory.Exists(fullPath))
            {
                return ScanOneResult.Skipped(ScanStatus.SkippedNotARegularFile, attributeNames, message: "Path points to a directory.");
            }

            await using var stream = new FileStream(
                fullPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete,
                options.BufferSizeBytes,
                FileOptions.SequentialScan | FileOptions.Asynchronous);

            using var sha = SHA256.Create();
            var buffer = new byte[options.BufferSizeBytes];
            long totalRead = 0;
            while (true)
            {
                var read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false);
                if (read == 0)
                {
                    break;
                }

                totalRead += read;
                sha.TransformBlock(buffer, 0, read, null, 0);
            }

            sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            var hash = Convert.ToHexString(sha.Hash!).ToLowerInvariant();
            var info = new FileInfo(fullPath);

            return ScanOneResult.Hashed(
                new ManifestRecord
                {
                    RunId = runId,
                    Path = fullPath,
                    Length = info.Length,
                    Sha256 = hash,
                    CreationTimeUtc = info.CreationTimeUtc,
                    LastWriteTimeUtc = info.LastWriteTimeUtc,
                    Attributes = attributeNames
                },
                totalRead);
        }
        catch (UnauthorizedAccessException ex)
        {
            return ScanOneResult.Skipped(ScanStatus.SkippedUnauthorized, Array.Empty<string>(), ex.GetType().Name, ex.Message);
        }
        catch (IOException ex)
        {
            var status = IOExceptionClassifier.Classify(ex);
            return status == ScanStatus.SkippedLocked
                ? ScanOneResult.Skipped(status, Array.Empty<string>(), ex.GetType().Name, ex.Message)
                : ScanOneResult.Failed(status, Array.Empty<string>(), ex.GetType().Name, ex.Message);
        }
        catch (Exception ex)
        {
            return ScanOneResult.Failed(ScanStatus.FailedUnexpected, Array.Empty<string>(), ex.GetType().Name, ex.Message);
        }
    }

    private static string[] GetAttributeNames(FileAttributes attributes)
    {
        return Enum.GetValues<FileAttributes>()
            .Where(a => a != 0 && attributes.HasFlag(a))
            .Select(a => a.ToString())
            .ToArray();
    }

    private sealed record ScanOneResult(
        ScanStatus Status,
        ManifestRecord? ManifestRecord,
        long ReadBytes,
        long? Length,
        string? Sha256,
        string[] Attributes,
        string? ExceptionType,
        string? Message)
    {
        public static ScanOneResult Hashed(ManifestRecord manifestRecord, long readBytes)
            => new(
                ScanStatus.Hashed,
                manifestRecord,
                readBytes,
                manifestRecord.Length,
                manifestRecord.Sha256,
                manifestRecord.Attributes,
                null,
                null);

        public static ScanOneResult Skipped(ScanStatus status, string[] attributes, string? exceptionType = null, string? message = null)
            => new(status, null, 0, null, null, attributes, exceptionType, message);

        public static ScanOneResult Failed(ScanStatus status, string[] attributes, string? exceptionType = null, string? message = null)
            => new(status, null, 0, null, null, attributes, exceptionType, message);
    }
}
