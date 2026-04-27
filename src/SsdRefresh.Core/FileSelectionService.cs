using System.Text;

namespace SsdRefresh.Core;

public sealed class FileSelectionService
{
    public async IAsyncEnumerable<string> SelectFilesAsync(ScanOptions options, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(options.FileListPath))
        {
            await foreach (var path in ExpandFileListAsync(options.FileListPath, options.Recursive, cancellationToken))
            {
                foreach (var file in ExpandPath(path, options.Recursive, options.IncludeReparsePoints))
                {
                    yield return file;
                }
            }

            yield break;
        }

        if (string.IsNullOrWhiteSpace(options.Path))
        {
            yield break;
        }

        foreach (var file in ExpandPath(options.Path, options.Recursive, options.IncludeReparsePoints))
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return file;
        }
    }

    private static IEnumerable<string> ExpandPath(string inputPath, bool recursive, bool includeReparsePoints)
    {
        var fullPath = System.IO.Path.GetFullPath(inputPath);
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

        var option = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        IEnumerable<string> files;
        try
        {
            files = Directory.EnumerateFiles(fullPath, "*", option);
        }
        catch
        {
            yield break;
        }

        foreach (var file in files)
        {
            if (!includeReparsePoints)
            {
                FileAttributes attrs;
                try { attrs = File.GetAttributes(file); } catch { continue; }
                if (attrs.HasFlag(FileAttributes.ReparsePoint))
                {
                    continue;
                }
            }

            yield return file;
        }
    }

    private static async IAsyncEnumerable<string> ExpandFileListAsync(string fileListPath, bool recursive, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(System.IO.Path.GetFullPath(fileListPath));
        using var reader = new StreamReader(stream, Encoding.UTF8, true);
        while (!reader.EndOfStream)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var line = await reader.ReadLineAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var trimmed = line.Trim();
            if (trimmed.StartsWith('#'))
            {
                continue;
            }

            yield return trimmed;
        }
    }
}
