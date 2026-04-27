using System.Text.Json;
using Shouldly;
using SsdRefresh.Core;

namespace SsdRefresh.Core.Tests;

public sealed class FileScanServiceTests
{
    [Fact]
    public async Task ScanFile_existingFile_shouldWriteManifestAndReportRecord()
    {
        using var env = new TestEnvironment();
        var filePath = env.CreateFile("file.txt", "hello");
        var manifestPath = env.PathOf("manifest.jsonl");
        var reportPath = env.PathOf("report.jsonl");

        var service = new FileScanService(new FileSelectionService());
        var result = await service.ScanSingleFileAsync(filePath, CreateOptions(manifestPath, reportPath), null, CancellationToken.None);

        result.Summary.Hashed.ShouldBe(1);
        File.ReadAllLines(manifestPath).Length.ShouldBe(1);
        File.ReadAllLines(reportPath).Length.ShouldBe(1);
    }

    [Fact]
    public async Task ScanFile_readOnlyFile_shouldHashSuccessfully()
    {
        using var env = new TestEnvironment();
        var filePath = env.CreateFile("readonly.txt", "payload", FileAttributes.ReadOnly);
        var service = new FileScanService(new FileSelectionService());

        var result = await service.ScanSingleFileAsync(filePath, CreateOptions(env.PathOf("m.jsonl"), env.PathOf("r.jsonl")), null, CancellationToken.None);

        result.Summary.Hashed.ShouldBe(1);
    }

    [Fact]
    public async Task ScanFile_missingFile_shouldReportSkippedFileNotFound()
    {
        using var env = new TestEnvironment();
        var missing = env.PathOf("missing.txt");
        var report = env.PathOf("report.jsonl");
        var service = new FileScanService(new FileSelectionService());

        _ = await service.ScanSingleFileAsync(missing, CreateOptions(env.PathOf("manifest.jsonl"), report), null, CancellationToken.None);

        var line = File.ReadAllLines(report).Single();
        var record = JsonSerializer.Deserialize<ReportRecord>(line)!;
        record.Status.ShouldBe(ScanStatus.SkippedFileNotFound);
    }

    [Fact]
    public async Task ScanFile_lockedFile_shouldReportSkippedLocked()
    {
        using var env = new TestEnvironment();
        var file = env.CreateFile("locked.txt", "locked-content");
        using var _ = new FileStream(file, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        var service = new FileScanService(new FileSelectionService());

        await service.ScanSingleFileAsync(file, CreateOptions(env.PathOf("m.jsonl"), env.PathOf("r.jsonl")), null, CancellationToken.None);

        var line = File.ReadAllLines(env.PathOf("r.jsonl")).Single();
        var record = JsonSerializer.Deserialize<ReportRecord>(line)!;
        record.Status.ShouldBe(ScanStatus.SkippedLocked);
    }

    [Fact]
    public async Task ScanDirectory_withoutRecursive_shouldScanOnlyDirectFiles()
    {
        using var env = new TestEnvironment();
        env.CreateFile("root-a.txt", "a");
        env.CreateFile("sub/root-b.txt", "b");

        var service = new FileScanService(new FileSelectionService());
        var result = await service.ScanPathAsync(env.RootPath, CreateOptions(env.PathOf("m.jsonl"), env.PathOf("r.jsonl")), null, CancellationToken.None);

        result.Summary.FilesSeen.ShouldBe(1);
    }

    [Fact]
    public async Task ScanDirectory_withRecursive_shouldScanNestedFiles()
    {
        using var env = new TestEnvironment();
        env.CreateFile("root-a.txt", "a");
        env.CreateFile("sub/root-b.txt", "b");

        var service = new FileScanService(new FileSelectionService());
        var options = CreateOptions(env.PathOf("m.jsonl"), env.PathOf("r.jsonl")) with { Recursive = true };
        var result = await service.ScanPathAsync(env.RootPath, options, null, CancellationToken.None);

        result.Summary.FilesSeen.ShouldBe(2);
    }

    [Fact]
    public async Task ScanDirectory_reparsePointByDefault_shouldSkipReparsePoint()
    {
        using var env = new TestEnvironment();
        var target = env.CreateFile("target.txt", "x");
        var linkPath = env.PathOf("link.txt");
        try
        {
            File.CreateSymbolicLink(linkPath, target);
        }
        catch (Exception)
        {
            return;
        }

        var service = new FileScanService(new FileSelectionService());
        await service.ScanSingleFileAsync(linkPath, CreateOptions(env.PathOf("m.jsonl"), env.PathOf("r.jsonl")), null, CancellationToken.None);

        var record = JsonSerializer.Deserialize<ReportRecord>(File.ReadAllLines(env.PathOf("r.jsonl")).Single())!;
        record.Status.ShouldBe(ScanStatus.SkippedReparsePoint);
    }

    [Fact]
    public async Task ScanFile_hiddenFileByDefault_shouldSkipUnsupportedAttribute()
    {
        using var env = new TestEnvironment();
        var path = env.CreateFile("hidden.txt", "h");
        File.SetAttributes(path, File.GetAttributes(path) | FileAttributes.Hidden);

        var service = new FileScanService(new FileSelectionService());
        await service.ScanSingleFileAsync(path, CreateOptions(env.PathOf("m.jsonl"), env.PathOf("r.jsonl")), null, CancellationToken.None);

        var record = JsonSerializer.Deserialize<ReportRecord>(File.ReadAllLines(env.PathOf("r.jsonl")).Single())!;
        record.Status.ShouldBe(ScanStatus.SkippedUnsupportedAttribute);
    }

    [Fact]
    public async Task ScanFile_hiddenFileWithIncludeHidden_shouldHashSuccessfully()
    {
        using var env = new TestEnvironment();
        var path = env.CreateFile("hidden2.txt", "h");
        File.SetAttributes(path, File.GetAttributes(path) | FileAttributes.Hidden);

        var service = new FileScanService(new FileSelectionService());
        var options = CreateOptions(env.PathOf("m.jsonl"), env.PathOf("r.jsonl")) with { IncludeHidden = true };
        var result = await service.ScanSingleFileAsync(path, options, null, CancellationToken.None);

        result.Summary.Hashed.ShouldBe(1);
    }

    [Fact]
    public async Task ScanFromFileList_commentsAndEmptyLines_shouldIgnoreThoseLines()
    {
        using var env = new TestEnvironment();
        var file1 = env.CreateFile("a.txt", "a");
        _ = env.CreateFile("b.txt", "b");
        var fileList = env.PathOf("list.txt");
        await File.WriteAllTextAsync(fileList, $"# comment{Environment.NewLine}{Environment.NewLine}{file1}{Environment.NewLine}");

        var service = new FileScanService(new FileSelectionService());
        var result = await service.ScanFileListAsync(fileList, CreateOptions(env.PathOf("m.jsonl"), env.PathOf("r.jsonl")), null, CancellationToken.None);

        result.Summary.FilesSeen.ShouldBe(1);
    }

    private static ScanOptions CreateOptions(string manifestPath, string reportPath) => new()
    {
        ManifestPath = manifestPath,
        ReportPath = reportPath
    };

    private sealed class TestEnvironment : IDisposable
    {
        public string RootPath { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ssdrefresh-tests", Guid.NewGuid().ToString("N"));

        public TestEnvironment()
        {
            Directory.CreateDirectory(RootPath);
        }

        public string PathOf(string relativePath) => System.IO.Path.Combine(RootPath, relativePath);

        public string CreateFile(string relativePath, string content, FileAttributes? attributes = null)
        {
            var path = PathOf(relativePath);
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content);
            if (attributes.HasValue)
            {
                File.SetAttributes(path, attributes.Value);
            }

            return path;
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(RootPath))
                {
                    Directory.Delete(RootPath, true);
                }
            }
            catch
            {
                // best effort cleanup for tests
            }
        }
    }
}
