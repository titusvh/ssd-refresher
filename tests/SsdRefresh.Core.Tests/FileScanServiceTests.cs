using System.Text.Json;
using Shouldly;
using SsdRefresh.Core;

namespace SsdRefresh.Core.Tests;

public sealed class FileScanServiceTests : IDisposable
{
    private readonly string _root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"ssdrefresh-tests-{Guid.NewGuid():N}");

    public FileScanServiceTests()
    {
        Directory.CreateDirectory(_root);
    }

    [Fact]
    public async Task ScanFile_existingFile_shouldWriteManifestAndReportRecord()
    {
        var file = CreateFile("a.txt", "hello");
        var (manifest, report) = GetOutputs();

        var service = new FileScanService();
        await service.ScanAsync(new ScanOptions(file, null, manifest, report, false, false, false, false));

        var manifestLines = await File.ReadAllLinesAsync(manifest);
        var reportLines = await File.ReadAllLinesAsync(report);
        manifestLines.Length.ShouldBe(1);
        reportLines.Length.ShouldBe(1);

        var manifestRecord = JsonSerializer.Deserialize<ManifestRecord>(manifestLines[0]);
        manifestRecord.ShouldNotBeNull();
        manifestRecord.Status.ShouldBe(ScanStatus.Hashed);
    }

    [Fact]
    public async Task ScanFile_readOnlyFile_shouldHashSuccessfully()
    {
        var file = CreateFile("readonly.txt", "hello");
        File.SetAttributes(file, File.GetAttributes(file) | FileAttributes.ReadOnly);
        var (manifest, report) = GetOutputs();

        await new FileScanService().ScanAsync(new ScanOptions(file, null, manifest, report, false, false, false, false));
        var line = (await File.ReadAllLinesAsync(report)).Single();
        JsonSerializer.Deserialize<ReportRecord>(line)!.Status.ShouldBe(ScanStatus.Hashed);
    }

    [Fact]
    public async Task ScanFile_missingFile_shouldReportSkippedFileNotFound()
    {
        var missing = System.IO.Path.Combine(_root, "missing.txt");
        var (manifest, report) = GetOutputs();

        await new FileScanService().ScanAsync(new ScanOptions(missing, null, manifest, report, false, false, false, false));
        (await File.ReadAllLinesAsync(manifest)).ShouldBeEmpty();
        JsonSerializer.Deserialize<ReportRecord>((await File.ReadAllLinesAsync(report)).Single())!.Status.ShouldBe(ScanStatus.SkippedFileNotFound);
    }

    [Fact]
    public async Task ScanFile_lockedFile_shouldReportSkippedLocked()
    {
        var file = CreateFile("locked.txt", "lock");
        var (manifest, report) = GetOutputs();

        using var lockStream = new FileStream(file, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        await new FileScanService().ScanAsync(new ScanOptions(file, null, manifest, report, false, false, false, false));

        JsonSerializer.Deserialize<ReportRecord>((await File.ReadAllLinesAsync(report)).Single())!.Status.ShouldBe(ScanStatus.SkippedLocked);
    }

    [Fact]
    public async Task ScanDirectory_withoutRecursive_shouldScanOnlyDirectFiles()
    {
        CreateFile("root.txt", "1");
        Directory.CreateDirectory(System.IO.Path.Combine(_root, "sub"));
        CreateFile("sub/nested.txt", "2");
        var (manifest, report) = GetOutputs();

        await new FileScanService().ScanAsync(new ScanOptions(_root, null, manifest, report, false, false, false, false));

        (await File.ReadAllLinesAsync(report)).Length.ShouldBe(1);
    }

    [Fact]
    public async Task ScanDirectory_withRecursive_shouldScanNestedFiles()
    {
        CreateFile("root2.txt", "1");
        Directory.CreateDirectory(System.IO.Path.Combine(_root, "sub2"));
        CreateFile("sub2/nested2.txt", "2");
        var (manifest, report) = GetOutputs();

        await new FileScanService().ScanAsync(new ScanOptions(_root, null, manifest, report, true, false, false, false));

        (await File.ReadAllLinesAsync(report)).Length.ShouldBeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task ScanDirectory_reparsePointByDefault_shouldSkipReparsePoint()
    {
        var target = CreateFile("target.txt", "x");
        var linkPath = System.IO.Path.Combine(_root, "link.txt");
        try
        {
            File.CreateSymbolicLink(linkPath, target);
        }
        catch
        {
            return;
        }

        var (manifest, report) = GetOutputs();
        await new FileScanService().ScanAsync(new ScanOptions(linkPath, null, manifest, report, false, false, false, false));

        JsonSerializer.Deserialize<ReportRecord>((await File.ReadAllLinesAsync(report)).Single())!.Status.ShouldBe(ScanStatus.SkippedReparsePoint);
    }

    [Fact]
    public async Task ScanFile_hiddenFileByDefault_shouldSkipUnsupportedAttribute()
    {
        var file = CreateFile("hidden.txt", "x");
        File.SetAttributes(file, File.GetAttributes(file) | FileAttributes.Hidden);
        var (manifest, report) = GetOutputs();

        await new FileScanService().ScanAsync(new ScanOptions(file, null, manifest, report, false, false, false, false));
        JsonSerializer.Deserialize<ReportRecord>((await File.ReadAllLinesAsync(report)).Single())!.Status.ShouldBe(ScanStatus.SkippedUnsupportedAttribute);
    }

    [Fact]
    public async Task ScanFile_hiddenFileWithIncludeHidden_shouldHashSuccessfully()
    {
        var file = CreateFile("hidden-include.txt", "x");
        File.SetAttributes(file, File.GetAttributes(file) | FileAttributes.Hidden);
        var (manifest, report) = GetOutputs();

        await new FileScanService().ScanAsync(new ScanOptions(file, null, manifest, report, false, true, false, false));
        JsonSerializer.Deserialize<ReportRecord>((await File.ReadAllLinesAsync(report)).Single())!.Status.ShouldBe(ScanStatus.Hashed);
    }

    [Fact]
    public async Task ScanFromFileList_commentsAndEmptyLines_shouldIgnoreThoseLines()
    {
        var a = CreateFile("list-a.txt", "a");
        var b = CreateFile("list-b.txt", "b");
        var fileList = System.IO.Path.Combine(_root, "files.txt");
        await File.WriteAllTextAsync(fileList, $"#comment\n\n{a}\n   \n{b}\n");
        var (manifest, report) = GetOutputs();

        await new FileScanService().ScanAsync(new ScanOptions(null, fileList, manifest, report, false, false, false, false));

        (await File.ReadAllLinesAsync(report)).Length.ShouldBe(2);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, true);
        }
    }

    private string CreateFile(string relativePath, string contents)
    {
        var fullPath = System.IO.Path.Combine(_root, relativePath.Replace('/', System.IO.Path.DirectorySeparatorChar));
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, contents);
        return fullPath;
    }

    private (string manifest, string report) GetOutputs()
    {
        return (System.IO.Path.Combine(_root, $"manifest-{Guid.NewGuid():N}.jsonl"), System.IO.Path.Combine(_root, $"report-{Guid.NewGuid():N}.jsonl"));
    }
}
