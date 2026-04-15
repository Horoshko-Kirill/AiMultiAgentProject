using System.Text.Json;
using AiMultiAgent.Stats.Contracts.Pm;
using AiMultiAgent.Stats.Service.Data;
using AiMultiAgent.Stats.Service.Models;

namespace AiMultiAgent.Stats.Service.Services;

public sealed class PmReportProjector
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public RunProjection ProjectSuccess(
        PmRequestWire request,
        string rawRequestJson,
        string rawReportJson,
        DateTimeOffset createdAtUtc,
        long durationMs,
        int? upstreamStatusCode)
    {
        var report = JsonSerializer.Deserialize<PmReportWire>(rawReportJson, JsonOptions)
            ?? new PmReportWire();

        var runId = Guid.NewGuid();
        var fileInputs = request.Files ?? [];
        var totalChars = fileInputs.Sum(x => x.Data?.Length ?? 0);
        var totalLines = fileInputs.Sum(x => CountLines(x.Data));

        var fileMetrics = fileInputs.ToDictionary(
            x => x.FileName,
            x => new StatsFileEntity
            {
                Id = Guid.NewGuid(),
                RunId = runId,
                FileName = x.FileName,
                CharCount = x.Data?.Length ?? 0,
                LineCount = CountLines(x.Data),
                ContainsTodo = Contains(x.Data, "TODO"),
                ContainsFixme = Contains(x.Data, "FIXME"),
                ContainsPotentialSecret =
                    Contains(x.Data, "password") ||
                    Contains(x.Data, "apikey") ||
                    Contains(x.Data, "api_key") ||
                    Contains(x.Data, "token")
            },
            StringComparer.OrdinalIgnoreCase);

        var toolDurations = ExtractToolDurations(report.Trace);

        var tools = new List<StatsToolExecutionEntity>();
        var codeReviewResults = ParseCodeReviewResults(report);

        for (var i = 0; i < codeReviewResults.Count; i++)
        {
            var item = codeReviewResults[i];
            long? reviewDuration = i < toolDurations.CodeReviewDurations.Count
                ? toolDurations.CodeReviewDurations[i]
                : null;

            if (fileMetrics.TryGetValue(item.FileName, out var file))
            {
                file.TruncatedForLlm = item.Truncated;
                file.UsedFallback = item.UsedFallback;
                file.IssueInfoCount = item.InfoCount;
                file.IssueWarningCount = item.WarningCount;
                file.IssueErrorCount = item.ErrorCount;
                file.SuggestionsCount = item.SuggestionsCount;
                file.ReviewDurationMs = reviewDuration;
            }

            tools.Add(new StatsToolExecutionEntity
            {
                Id = Guid.NewGuid(),
                RunId = runId,
                ToolName = "code_review",
                Sequence = i + 1,
                Label = item.FileName,
                Succeeded = string.IsNullOrWhiteSpace(item.Error),
                UsedFallback = item.UsedFallback,
                DurationMs = reviewDuration,
                Details = item.Error ?? (item.UsedFallback ? "fallback" : "ok")
            });
        }

        var docsResult = ParseGenerateDocsResult(report);
        tools.Add(new StatsToolExecutionEntity
        {
            Id = Guid.NewGuid(),
            RunId = runId,
            ToolName = "generate_docs",
            Sequence = 1,
            Label = request.ComponentName,
            Succeeded = string.IsNullOrWhiteSpace(docsResult.Error),
            UsedFallback = docsResult.UsedFallback,
            DurationMs = toolDurations.GenerateDocsDuration,
            Details = docsResult.Error ?? (docsResult.UsedFallback ? "fallback" : "ok")
        });

        var issuesInfo = fileMetrics.Values.Sum(x => x.IssueInfoCount);
        var issuesWarning = fileMetrics.Values.Sum(x => x.IssueWarningCount);
        var issuesError = fileMetrics.Values.Sum(x => x.IssueErrorCount);
        var suggestions = fileMetrics.Values.Sum(x => x.SuggestionsCount);

        var aggregationMode = report.Meta.TryGetValue("aggregation", out var modeNode) && modeNode.ValueKind == JsonValueKind.String
            ? modeNode.GetString() ?? "unknown"
            : "unknown";

        var run = new StatsRunEntity
        {
            Id = runId,
            CreatedAtUtc = createdAtUtc,
            ComponentName = request.ComponentName?.Trim() ?? GuessComponentName(fileInputs),
            FilesCount = fileInputs.Count,
            TotalInputChars = totalChars,
            TotalInputLines = totalLines,
            DurationMs = durationMs,
            AggregationMode = aggregationMode,
            UsedAnyFallback = tools.Any(x => x.UsedFallback) || !string.Equals(aggregationMode, "llm", StringComparison.OrdinalIgnoreCase),
            IsGatewayFailure = false,
            UpstreamStatusCode = upstreamStatusCode,
            FailureReason = null,
            IssuesInfoCount = issuesInfo,
            IssuesWarningCount = issuesWarning,
            IssuesErrorCount = issuesError,
            IssuesTotal = issuesInfo + issuesWarning + issuesError,
            SuggestionsTotal = suggestions,
            TraceEventsCount = report.Trace?.Count ?? 0,
            Summary = report.Summary ?? "",
            RawRequestJson = rawRequestJson,
            RawReportJson = rawReportJson
        };

        return new RunProjection
        {
            Run = run,
            Files = fileMetrics.Values.OrderBy(x => x.FileName, StringComparer.OrdinalIgnoreCase).ToList(),
            Tools = tools.OrderBy(x => x.ToolName).ThenBy(x => x.Sequence).ToList()
        };
    }

    public RunProjection ProjectFailure(
        PmRequestWire request,
        string rawRequestJson,
        string? rawReportJson,
        DateTimeOffset createdAtUtc,
        long durationMs,
        int? upstreamStatusCode,
        string failureReason)
    {
        var runId = Guid.NewGuid();
        var files = (request.Files ?? []).Select(x => new StatsFileEntity
        {
            Id = Guid.NewGuid(),
            RunId = runId,
            FileName = x.FileName,
            CharCount = x.Data?.Length ?? 0,
            LineCount = CountLines(x.Data),
            ContainsTodo = Contains(x.Data, "TODO"),
            ContainsFixme = Contains(x.Data, "FIXME"),
            ContainsPotentialSecret =
                Contains(x.Data, "password") ||
                Contains(x.Data, "apikey") ||
                Contains(x.Data, "api_key") ||
                Contains(x.Data, "token")
        }).ToList();

        var run = new StatsRunEntity
        {
            Id = runId,
            CreatedAtUtc = createdAtUtc,
            ComponentName = request.ComponentName?.Trim() ?? GuessComponentName(request.Files ?? []),
            FilesCount = request.Files?.Count ?? 0,
            TotalInputChars = (request.Files ?? []).Sum(x => x.Data?.Length ?? 0),
            TotalInputLines = (request.Files ?? []).Sum(x => CountLines(x.Data)),
            DurationMs = durationMs,
            AggregationMode = "gateway_error",
            UsedAnyFallback = false,
            IsGatewayFailure = true,
            UpstreamStatusCode = upstreamStatusCode,
            FailureReason = failureReason,
            IssuesInfoCount = 0,
            IssuesWarningCount = 0,
            IssuesErrorCount = 0,
            IssuesTotal = 0,
            SuggestionsTotal = 0,
            TraceEventsCount = 0,
            Summary = "Proxy run failed before statistics could parse a valid PM report.",
            RawRequestJson = rawRequestJson,
            RawReportJson = rawReportJson
        };

        return new RunProjection
        {
            Run = run,
            Files = files,
            Tools = []
        };
    }

    private static string GuessComponentName(IEnumerable<PmFileWire> files)
    {
        var first = files.FirstOrDefault()?.FileName;
        if (string.IsNullOrWhiteSpace(first))
        {
            return "Component";
        }

        return Path.GetFileNameWithoutExtension(first);
    }

    private static int CountLines(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0;
        }

        return text.Count(static c => c == '\n') + 1;
    }

    private static bool Contains(string? text, string value)
        => !string.IsNullOrWhiteSpace(text) &&
           text.Contains(value, StringComparison.OrdinalIgnoreCase);

    private static ToolDurationProjection ExtractToolDurations(IEnumerable<TraceEventWire>? trace)
    {
        var starts = new Dictionary<string, Queue<TraceEventWire>>(StringComparer.OrdinalIgnoreCase);
        var result = new ToolDurationProjection();

        foreach (var item in (trace ?? []).OrderBy(x => x.Ts))
        {
            if (string.IsNullOrWhiteSpace(item.Tool))
            {
                continue;
            }

            if (string.Equals(item.Type, "TOOL_CALL_START", StringComparison.OrdinalIgnoreCase))
            {
                if (!starts.TryGetValue(item.Tool, out var queue))
                {
                    queue = new Queue<TraceEventWire>();
                    starts[item.Tool] = queue;
                }

                queue.Enqueue(item);
                continue;
            }

            if (!string.Equals(item.Type, "TOOL_CALL_END", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!starts.TryGetValue(item.Tool, out var existing) || existing.Count == 0)
            {
                continue;
            }

            var start = existing.Dequeue();
            var duration = Math.Max(0, (long)(item.Ts - start.Ts).TotalMilliseconds);

            if (string.Equals(item.Tool, "code_review", StringComparison.OrdinalIgnoreCase))
            {
                result.CodeReviewDurations.Add(duration);
            }
            else if (string.Equals(item.Tool, "generate_docs", StringComparison.OrdinalIgnoreCase))
            {
                result.GenerateDocsDuration = duration;
            }
        }

        return result;
    }

    private static List<CodeReviewProjection> ParseCodeReviewResults(PmReportWire report)
    {
        var output = new List<CodeReviewProjection>();

        if (!report.ToolResults.TryGetValue("code_review", out var node) || node.ValueKind != JsonValueKind.Array)
        {
            return output;
        }

        foreach (var item in node.EnumerateArray())
        {
            var fileName = item.TryGetProperty("fileName", out var fileNode) && fileNode.ValueKind == JsonValueKind.String
                ? fileNode.GetString() ?? "unknown"
                : "unknown";

            var usedFallback = item.TryGetProperty("usedFallback", out var fallbackNode) &&
                               fallbackNode.ValueKind is JsonValueKind.True or JsonValueKind.False &&
                               fallbackNode.GetBoolean();

            var truncated = item.TryGetProperty("truncated", out var truncNode) &&
                            truncNode.ValueKind is JsonValueKind.True or JsonValueKind.False &&
                            truncNode.GetBoolean();

            var error = item.TryGetProperty("error", out var errNode) && errNode.ValueKind == JsonValueKind.String
                ? errNode.GetString()
                : null;

            var info = 0;
            var warning = 0;
            var err = 0;
            var suggestions = 0;

            if (item.TryGetProperty("result", out var resultNode) && resultNode.ValueKind == JsonValueKind.Object)
            {
                if (TryGetPropertyIgnoreCase(resultNode, "Issues", out var issuesNode) && issuesNode.ValueKind == JsonValueKind.Array)
                {
                    foreach (var issue in issuesNode.EnumerateArray())
                    {
                        var severity = TryGetPropertyIgnoreCase(issue, "Severity", out var sevNode) && sevNode.ValueKind == JsonValueKind.String
                            ? sevNode.GetString()
                            : "info";

                        switch ((severity ?? "info").ToLowerInvariant())
                        {
                            case "error":
                                err++;
                                break;
                            case "warning":
                                warning++;
                                break;
                            default:
                                info++;
                                break;
                        }
                    }
                }

                if (TryGetPropertyIgnoreCase(resultNode, "Suggestions", out var suggNode))
                {
                    suggestions = suggNode.ValueKind == JsonValueKind.Array ? suggNode.GetArrayLength() : 0;
                }
            }

            output.Add(new CodeReviewProjection
            {
                FileName = fileName,
                UsedFallback = usedFallback,
                Truncated = truncated,
                Error = error,
                InfoCount = info,
                WarningCount = warning,
                ErrorCount = err,
                SuggestionsCount = suggestions
            });
        }

        return output;
    }

    private static DocsProjection ParseGenerateDocsResult(PmReportWire report)
    {
        if (!report.ToolResults.TryGetValue("generate_docs", out var node) || node.ValueKind != JsonValueKind.Object)
        {
            return new DocsProjection();
        }

        var usedFallback = node.TryGetProperty("usedFallback", out var usedNode) &&
                           usedNode.ValueKind is JsonValueKind.True or JsonValueKind.False &&
                           usedNode.GetBoolean();

        var error = node.TryGetProperty("error", out var errNode) && errNode.ValueKind == JsonValueKind.String
            ? errNode.GetString()
            : null;

        return new DocsProjection
        {
            UsedFallback = usedFallback,
            Error = error
        };
    }

    private static bool TryGetPropertyIgnoreCase(JsonElement node, string propertyName, out JsonElement value)
    {
        if (node.ValueKind != JsonValueKind.Object)
        {
            value = default;
            return false;
        }

        foreach (var property in node.EnumerateObject())
        {
            if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private sealed class ToolDurationProjection
    {
        public List<long> CodeReviewDurations { get; } = [];
        public long? GenerateDocsDuration { get; set; }
    }

    private sealed class CodeReviewProjection
    {
        public string FileName { get; init; } = "";
        public bool UsedFallback { get; init; }
        public bool Truncated { get; init; }
        public string? Error { get; init; }
        public int InfoCount { get; init; }
        public int WarningCount { get; init; }
        public int ErrorCount { get; init; }
        public int SuggestionsCount { get; init; }
    }

    private sealed class DocsProjection
    {
        public bool UsedFallback { get; init; }
        public string? Error { get; init; }
    }
}
