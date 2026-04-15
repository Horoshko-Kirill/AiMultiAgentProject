using Microsoft.EntityFrameworkCore;

namespace AiMultiAgent.Stats.Service.Data;

public sealed class StatsRunEntity
{
    public Guid Id { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }

    public string ComponentName { get; set; } = "";
    public int FilesCount { get; set; }
    public int TotalInputChars { get; set; }
    public int TotalInputLines { get; set; }

    public long DurationMs { get; set; }

    public string AggregationMode { get; set; } = "";
    public bool UsedAnyFallback { get; set; }
    public bool IsGatewayFailure { get; set; }
    public int? UpstreamStatusCode { get; set; }
    public string? FailureReason { get; set; }

    public int IssuesInfoCount { get; set; }
    public int IssuesWarningCount { get; set; }
    public int IssuesErrorCount { get; set; }
    public int IssuesTotal { get; set; }
    public int SuggestionsTotal { get; set; }

    public int TraceEventsCount { get; set; }

    public string Summary { get; set; } = "";

    public string? RawRequestJson { get; set; }
    public string? RawReportJson { get; set; }

    public List<StatsFileEntity> Files { get; set; } = [];
    public List<StatsToolExecutionEntity> Tools { get; set; } = [];
}

public sealed class StatsFileEntity
{
    public Guid Id { get; set; }
    public Guid RunId { get; set; }

    public string FileName { get; set; } = "";
    public int CharCount { get; set; }
    public int LineCount { get; set; }

    public bool ContainsTodo { get; set; }
    public bool ContainsFixme { get; set; }
    public bool ContainsPotentialSecret { get; set; }

    public bool TruncatedForLlm { get; set; }
    public bool UsedFallback { get; set; }

    public int IssueInfoCount { get; set; }
    public int IssueWarningCount { get; set; }
    public int IssueErrorCount { get; set; }
    public int SuggestionsCount { get; set; }

    public long? ReviewDurationMs { get; set; }

    public StatsRunEntity? Run { get; set; }
}

public sealed class StatsToolExecutionEntity
{
    public Guid Id { get; set; }
    public Guid RunId { get; set; }

    public string ToolName { get; set; } = "";
    public int Sequence { get; set; }
    public string? Label { get; set; }

    public bool Succeeded { get; set; }
    public bool UsedFallback { get; set; }
    public long? DurationMs { get; set; }
    public string? Details { get; set; }

    public StatsRunEntity? Run { get; set; }
}
