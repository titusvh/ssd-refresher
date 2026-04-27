namespace SsdRefresh.Core;

public sealed class FileSelectionService
{
    public IEnumerable<string> SelectFromPath(string path, ScanOptions options)
    {
        var fullPath = System.IO.Path.GetFullPath(path);
        if (File.Exists(fullPath))
        {
            yield return fullPath;
            yield break;
        }

        if (!Directory.Exists(fullPath))
        {
            yield return fullPath;
            yield break;
        }

        var searchOption = options.Recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        foreach (var file in Directory.EnumerateFiles(fullPath, "*", searchOption))
        {
            yield return System.IO.Path.GetFullPath(file);
        }
    }

    public IEnumerable<string> SelectFromFileList(string fileListPath, ScanOptions options)
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
}
