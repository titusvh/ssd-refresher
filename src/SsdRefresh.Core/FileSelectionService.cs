namespace SsdRefresh.Core;

public sealed class FileSelectionService
{
    public IEnumerable<ScanTarget> SelectFromPath(string path, ScanOptions options)
    {
        var fullPath = System.IO.Path.GetFullPath(path);
        if (File.Exists(fullPath))
        {
            yield return new ScanTarget { Path = fullPath };
            yield break;
        }

        if (!Directory.Exists(fullPath))
        {
            yield return new ScanTarget { Path = fullPath };
            yield break;
        }

        foreach (var target in EnumerateDirectoryFiles(fullPath, options.Recursive))
        {
            yield return target;
        }
    }

    public IEnumerable<ScanTarget> SelectFromFileList(string fileListPath, ScanOptions options)
    {
        foreach (var rawLine in File.ReadLines(fileListPath))
        {
            var line = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
            {
                continue;
            }

            foreach (var candidate in SelectFromPath(line, options))
            {
                yield return candidate;
            }
        }
    }

    private static IEnumerable<ScanTarget> EnumerateDirectoryFiles(string rootPath, bool recursive)
    {
        var pendingDirectories = new Stack<string>();
        pendingDirectories.Push(rootPath);

        while (pendingDirectories.Count > 0)
        {
            var current = pendingDirectories.Pop();
            IEnumerable<string> entries;

            try
            {
                entries = Directory.GetFileSystemEntries(current, "*", SearchOption.TopDirectoryOnly);
            }
            catch (Exception ex)
            {
                yield return BuildEnumerationFailureTarget(current, ex);
                continue;
            }

            foreach (var entry in entries)
            {
                var fullEntryPath = System.IO.Path.GetFullPath(entry);

                if (File.Exists(fullEntryPath))
                {
                    yield return new ScanTarget { Path = fullEntryPath };
                    continue;
                }

                if (Directory.Exists(fullEntryPath) && recursive)
                {
                    pendingDirectories.Push(fullEntryPath);
                }
            }
        }
    }

    private static ScanTarget BuildEnumerationFailureTarget(string directoryPath, Exception ex)
    {
        var status = ex switch
        {
            UnauthorizedAccessException => ScanStatus.SkippedUnauthorized,
            IOException ioException => IOExceptionClassifier.Classify(ioException) == ScanStatus.SkippedLocked
                ? ScanStatus.SkippedLocked
                : ScanStatus.FailedIo,
            _ => ScanStatus.FailedUnexpected
        };

        return new ScanTarget
        {
            Path = directoryPath,
            PreScanStatus = status,
            ExceptionType = ex.GetType().Name,
            Message = ex.Message
        };
    }
}
