using System.Text.Json;
using Shouldly;
using SsdRefresh.Core;
using Xunit;

namespace SsdRefresh.Core.Tests;

public class JsonlSerializerTests
{
    [Fact]
    public void ManifestRecord_serialize_shouldUseCamelCasePropertyNames()
    {
        var record = CreateManifestRecord();

        var json = JsonlSerializer.SerializeManifest(record);
        using var doc = JsonDocument.Parse(json);

        doc.RootElement.TryGetProperty("schemaVersion", out _).ShouldBeTrue();
        doc.RootElement.TryGetProperty("runId", out _).ShouldBeTrue();
        doc.RootElement.TryGetProperty("lastWriteTimeUtc", out _).ShouldBeTrue();
        doc.RootElement.TryGetProperty("SchemaVersion", out _).ShouldBeFalse();
    }

    [Fact]
    public void ManifestRecord_serialize_shouldSerializeStatusAsString()
    {
        var record = CreateManifestRecord();
        record = new ManifestRecord
        {
            SchemaVersion = record.SchemaVersion,
            RunId = record.RunId,
            Operation = record.Operation,
            Path = record.Path,
            Length = record.Length,
            Sha256 = record.Sha256,
            CreationTimeUtc = record.CreationTimeUtc,
            LastWriteTimeUtc = record.LastWriteTimeUtc,
            Attributes = record.Attributes,
            Status = ScanStatus.SkippedLocked,
            DurationMs = record.DurationMs,
        };

        var json = JsonlSerializer.SerializeManifest(record);
        using var doc = JsonDocument.Parse(json);

        doc.RootElement.GetProperty("status").GetString().ShouldBe("SkippedLocked");
    }

    [Fact]
    public void ManifestRecord_deserialize_shouldRoundTripStatusString()
    {
        const string json = "{\"schemaVersion\":1,\"runId\":\"run-1\",\"operation\":\"Scan\",\"path\":\"C:/data.bin\",\"length\":5,\"sha256\":\"abc\",\"creationTimeUtc\":\"2026-01-01T00:00:00+00:00\",\"lastWriteTimeUtc\":\"2026-01-01T00:00:00+00:00\",\"attributes\":[],\"status\":\"FailedIo\",\"durationMs\":2}";

        var record = JsonlSerializer.DeserializeManifest(json);

        record.Status.ShouldBe(ScanStatus.FailedIo);
    }

    [Fact]
    public void ReportRecord_serialize_shouldUseCamelCasePropertyNames()
    {
        var record = CreateReportRecord();

        var json = JsonlSerializer.SerializeReport(record);
        using var doc = JsonDocument.Parse(json);

        doc.RootElement.TryGetProperty("schemaVersion", out _).ShouldBeTrue();
        doc.RootElement.TryGetProperty("exceptionType", out _).ShouldBeTrue();
        doc.RootElement.TryGetProperty("SchemaVersion", out _).ShouldBeFalse();
    }

    [Fact]
    public void ReportRecord_serialize_shouldSerializeStatusAsString()
    {
        var record = CreateReportRecord();
        record = new ReportRecord
        {
            SchemaVersion = record.SchemaVersion,
            RunId = record.RunId,
            Operation = record.Operation,
            Path = record.Path,
            Status = ScanStatus.FailedUnexpected,
            Length = record.Length,
            Sha256 = record.Sha256,
            Attributes = record.Attributes,
            ExceptionType = record.ExceptionType,
            Message = record.Message,
            DurationMs = record.DurationMs,
            TimestampUtc = record.TimestampUtc,
        };

        var json = JsonlSerializer.SerializeReport(record);
        using var doc = JsonDocument.Parse(json);

        doc.RootElement.GetProperty("status").GetString().ShouldBe("FailedUnexpected");
    }

    [Fact]
    public void ReportRecord_deserialize_shouldRoundTripStatusString()
    {
        const string json = "{\"schemaVersion\":1,\"runId\":\"run-1\",\"operation\":\"Scan\",\"path\":\"C:/data.bin\",\"status\":\"SkippedUnauthorized\",\"length\":null,\"attributes\":[],\"durationMs\":7,\"timestampUtc\":\"2026-01-01T00:00:00+00:00\"}";

        var record = JsonlSerializer.DeserializeReport(json);

        record.Status.ShouldBe(ScanStatus.SkippedUnauthorized);
    }

    [Fact]
    public void JsonlSerializer_serializeManifest_shouldNotContainNewline()
    {
        var json = JsonlSerializer.SerializeManifest(CreateManifestRecord());

        json.ShouldNotContain("\n");
        json.ShouldNotContain("\r");
    }

    [Fact]
    public void JsonlSerializer_serializeReport_shouldNotContainNewline()
    {
        var json = JsonlSerializer.SerializeReport(CreateReportRecord());

        json.ShouldNotContain("\n");
        json.ShouldNotContain("\r");
    }

    [Fact]
    public void ManifestRecord_defaults_shouldUseSchemaVersionOneAndOperationScan()
    {
        var record = new ManifestRecord
        {
            RunId = "run-1",
            Path = "C:/file.bin",
            Sha256 = "abc123",
        };

        record.SchemaVersion.ShouldBe(1);
        record.Operation.ShouldBe("Scan");
        record.Status.ShouldBe(ScanStatus.Hashed);
        record.Attributes.ShouldBeEmpty();
    }

    [Fact]
    public void ReportRecord_defaults_shouldUseSchemaVersionOneAndOperationScan()
    {
        var record = new ReportRecord
        {
            RunId = "run-1",
            Path = "C:/file.bin",
            Status = ScanStatus.Hashed,
        };

        record.SchemaVersion.ShouldBe(1);
        record.Operation.ShouldBe("Scan");
        record.Attributes.ShouldBeEmpty();
    }

    private static ManifestRecord CreateManifestRecord()
        => new()
        {
            RunId = "run-1",
            Path = "C:/data.bin",
            Length = 5,
            Sha256 = "abc",
            CreationTimeUtc = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            LastWriteTimeUtc = new DateTimeOffset(2026, 1, 2, 0, 0, 0, TimeSpan.Zero),
            Attributes = ["Archive"],
            Status = ScanStatus.Hashed,
            DurationMs = 1,
        };

    private static ReportRecord CreateReportRecord()
        => new()
        {
            RunId = "run-1",
            Path = "C:/data.bin",
            Status = ScanStatus.Hashed,
            Length = 5,
            Sha256 = "abc",
            Attributes = ["Archive"],
            ExceptionType = "IOException",
            Message = "message",
            DurationMs = 1,
            TimestampUtc = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
        };
}
