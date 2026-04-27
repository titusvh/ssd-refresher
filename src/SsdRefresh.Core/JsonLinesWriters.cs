using System.Text.Json;
using System.Text.Json.Serialization;

namespace SsdRefresh.Core;

public sealed class JsonLinesManifestWriter : IAsyncDisposable
{
    private readonly StreamWriter _writer;
    private readonly JsonSerializerOptions _jsonOptions;

    public JsonLinesManifestWriter(string path)
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(path))!);
        _writer = new StreamWriter(File.Open(path, FileMode.Create, FileAccess.Write, FileShare.Read));
        _jsonOptions = CreateOptions();
    }

    public async Task WriteAsync(ManifestRecord record, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(record, _jsonOptions);
        await _writer.WriteLineAsync(json.AsMemory(), cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _writer.FlushAsync();
        _writer.Dispose();
    }

    private static JsonSerializerOptions CreateOptions() => new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };
}

public sealed class JsonLinesReportWriter : IAsyncDisposable
{
    private readonly StreamWriter _writer;
    private readonly JsonSerializerOptions _jsonOptions;

    public JsonLinesReportWriter(string path)
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(path))!);
        _writer = new StreamWriter(File.Open(path, FileMode.Create, FileAccess.Write, FileShare.Read));
        _jsonOptions = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters = { new JsonStringEnumConverter() }
        };
    }

    public async Task WriteAsync(ReportRecord record, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(record, _jsonOptions);
        await _writer.WriteLineAsync(json.AsMemory(), cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _writer.FlushAsync();
        _writer.Dispose();
    }
}
