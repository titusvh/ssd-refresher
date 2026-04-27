using System.Diagnostics;
using System.Security.Cryptography;

namespace SsdRefresh.Core;

public sealed class FileScanService
{
    private readonly FileSelectionService _selectionService;

    public FileScanService(FileSelectionService? selectionService = null)
    {
        _selectionService = selectionService ?? new FileSelectionService();
    }

    public async Task<ScanResult> ScanAsync(ScanOptions options, IProgress<ScanProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        var runId = Guid.NewGuid();
        long filesSeen = 0;
        long filesHashed = 0;
        long filesSkipped = 0;
        long filesFailed = 0;
        long readBytes = 0;

        await using var manifestWriter = new JsonLinesManifestWriter(options.ManifestPath);
        await using var reportWriter = new JsonLinesReportWriter(options.ReportPath);

        await foreach (var file in _selectionService.SelectFilesAsync(options, cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var started = Stopwatch.StartNew();
            filesSeen++;
            ReportRecord report;

            try
            {
                var fullPath = System.IO.Path.GetFullPath(file);
                if (!File.Exists(fullPath))
                {
                    report = CreateReport(runId, fullPath, ScanStatus.SkippedFileNotFound, null, null, null, null, started.ElapsedMilliseconds);
                    filesSkipped++;
                }
                else
                {
                    var fileInfo = new FileInfo(fullPath);
                    var attributes = ToAttributeNames(fileInfo.Attributes);

                    if (ShouldSkip(fileInfo.Attributes, options, out var skipStatus))
                    {
                        report = CreateReport(runId, fullPath, skipStatus, fileInfo.Length, null, attributes, null, started.ElapsedMilliseconds);
                        filesSkipped++;
                    }
                    else
                    {
                        var hash = await HashFileAsync(fileInfo, options.BufferSizeBytes, cancellationToken);
                        readBytes += fileInfo.Length;
                        var manifest = new ManifestRecord
                        {
                            RunId = runId,
                            Path = fullPath,
                            Length = fileInfo.Length,
                            Sha256 = hash,
                            CreationTimeUtc = fileInfo.CreationTimeUtc,
                            LastWriteTimeUtc = fileInfo.LastWriteTimeUtc,
                            Attributes = attributes,
                            Status = ScanStatus.Hashed,
                            DurationMs = started.ElapsedMilliseconds
                        };

                        await manifestWriter.WriteAsync(manifest, cancellationToken);
                        report = CreateReport(runId, fullPath, ScanStatus.Hashed, fileInfo.Length, hash, attributes, null, started.ElapsedMilliseconds);
                        filesHashed++;
                    }
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                filesSkipped++;
                report = CreateReport(runId, file, ScanStatus.SkippedUnauthorized, null, null, [], ex, started.ElapsedMilliseconds);
            }
            catch (IOException ex)
            {
                var status = IOExceptionClassifier.Classify(ex);
                if (status == ScanStatus.SkippedLocked) filesSkipped++; else filesFailed++;
                report = CreateReport(runId, file, status, null, null, [], ex, started.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                filesFailed++;
                report = CreateReport(runId, file, ScanStatus.FailedUnexpected, null, null, [], ex, started.ElapsedMilliseconds);
            }

            await reportWriter.WriteAsync(report, cancellationToken);

            progress?.Report(new ScanProgress(filesSeen, filesHashed, filesSkipped, filesFailed, readBytes, file, DateTimeOffset.UtcNow));
        }

        return new ScanResult(runId, new ScanSummary(filesSeen, filesHashed, filesSkipped, filesFailed, readBytes), false);
    }

    private static bool ShouldSkip(FileAttributes attributes, ScanOptions options, out ScanStatus status)
    {
        if (!options.IncludeReparsePoints && attributes.HasFlag(FileAttributes.ReparsePoint))
        {
            status = ScanStatus.SkippedReparsePoint;
            return true;
        }

        if (!options.IncludeHidden && attributes.HasFlag(FileAttributes.Hidden))
        {
            status = ScanStatus.SkippedUnsupportedAttribute;
            return true;
        }

        if (!options.IncludeSystem && attributes.HasFlag(FileAttributes.System))
        {
            status = ScanStatus.SkippedUnsupportedAttribute;
            return true;
        }

        status = ScanStatus.Hashed;
        return false;
    }

    private static async Task<string> HashFileAsync(FileInfo fileInfo, int bufferSize, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            fileInfo.FullName,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete,
            bufferSize,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

        using var sha = SHA256.Create();
        var hash = await sha.ComputeHashAsync(stream, cancellationToken);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static IReadOnlyList<string> ToAttributeNames(FileAttributes attributes)
    {
        return Enum.GetValues<FileAttributes>()
            .Where(x => x != 0 && attributes.HasFlag(x))
            .Select(x => x.ToString())
            .ToArray();
    }

    private static ReportRecord CreateReport(Guid runId, string path, ScanStatus status, long? length, string? sha256, IReadOnlyList<string>? attributes, Exception? ex, long durationMs)
    {
        return new ReportRecord
        {
            RunId = runId,
            Path = System.IO.Path.GetFullPath(path),
            Status = status,
            Length = length,
            Sha256 = sha256,
            Attributes = attributes ?? [],
            ExceptionType = ex?.GetType().Name,
            Message = ex?.Message,
            DurationMs = durationMs,
            TimestampUtc = DateTimeOffset.UtcNow
        };
    }
}
