using System.Text.Json;
using System.Text.Json.Serialization;

namespace SsdRefresh.Core;

public static class JsonlSerializerOptions
{
    public static JsonSerializerOptions Instance { get; } = Create();

    private static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = false,
        };

        options.Converters.Add(new JsonStringEnumConverter());

        return options;
    }
}
