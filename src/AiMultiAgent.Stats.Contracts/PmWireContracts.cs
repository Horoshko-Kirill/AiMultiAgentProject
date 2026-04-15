using System.Text.Json;
using System.Text.Json.Serialization;

namespace AiMultiAgent.Stats.Contracts.Pm;

public sealed class PmRequestWire
{
    [JsonPropertyName("files")]
    public List<PmFileWire> Files { get; init; } = [];

    [JsonPropertyName("componentName")]
    public string? ComponentName { get; init; }

    [JsonPropertyName("componentDescription")]
    public string? ComponentDescription { get; init; }
}

public sealed class PmFileWire
{
    [JsonPropertyName("fileName")]
    public string FileName { get; init; } = "";

    [JsonPropertyName("data")]
    public string Data { get; init; } = "";
}

public sealed class PmReportWire
{
    [JsonPropertyName("meta")]
    public Dictionary<string, JsonElement> Meta { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    [JsonPropertyName("toolResults")]
    public Dictionary<string, JsonElement> ToolResults { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    [JsonPropertyName("risks")]
    public List<JsonElement> Risks { get; init; } = [];

    [JsonPropertyName("nextActions")]
    public List<JsonElement> NextActions { get; init; } = [];

    [JsonPropertyName("summary")]
    public string Summary { get; init; } = "";

    [JsonPropertyName("trace")]
    public List<TraceEventWire> Trace { get; init; } = [];
}

public sealed class TraceEventWire
{
    [JsonPropertyName("ts")]
    public DateTimeOffset Ts { get; init; }

    [JsonPropertyName("type")]
    public string Type { get; init; } = "";

    [JsonPropertyName("tool")]
    public string? Tool { get; init; }

    [JsonPropertyName("details")]
    public string? Details { get; init; }
}
