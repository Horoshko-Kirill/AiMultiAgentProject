using System.Text.Json.Serialization;

namespace AiMultiAgent.Stats.Contracts.Dashboard;

public sealed class DashboardOverviewDto
{
    public DateTimeOffset GeneratedAtUtc { get; init; }
    public DateTimeOffset? WindowStartUtc { get; init; }
    public DateTimeOffset? WindowEndUtc { get; init; }

    public int RunsCount { get; init; }
    public int ComponentsCount { get; init; }

    public RateBlockDto Rates { get; init; } = new();
    public NumericSummaryDto DurationMs { get; init; } = new();
    public NumericSummaryDto TotalInputChars { get; init; } = new();
    public NumericSummaryDto FilesPerRun { get; init; } = new();
    public NumericSummaryDto IssuesPerRun { get; init; } = new();
    public NumericSummaryDto ErrorsPerRun { get; init; } = new();
    public NumericSummaryDto WarningsPerRun { get; init; } = new();
    public NumericSummaryDto FileSizeChars { get; init; } = new();

    public DistributionDto SeverityDistribution { get; init; } = new();
    public DistributionDto AggregationModeDistribution { get; init; } = new();

    public HistogramDto DurationHistogram { get; init; } = new();
    public HistogramDto IssueHistogram { get; init; } = new();
    public HistogramDto FileSizeHistogram { get; init; } = new();

    public LinearTrendDto DurationTrend { get; init; } = new();
    public LinearTrendDto IssuesTrend { get; init; } = new();

    public List<CorrelationDto> Correlations { get; init; } = [];
    public OutlierSummaryDto Outliers { get; init; } = new();
    public ConcentrationDto Concentration { get; init; } = new();

    public List<ComponentStatsDto> TopComponents { get; init; } = [];
    public List<ToolStatsDto> Tools { get; init; } = [];
    public List<TimelinePointDto> Timeline { get; init; } = [];
    public List<RecentRunDto> RecentRuns { get; init; } = [];
}

public sealed class RateBlockDto
{
    public double SuccessRate { get; init; }
    public double FallbackRate { get; init; }
    public double GatewayFailureRate { get; init; }
    public double LlmAggregationRate { get; init; }
    public double WarningRunRate { get; init; }
    public double ErrorRunRate { get; init; }
    public ConfidenceIntervalDto FallbackRateConfidence95 { get; init; } = new();
}

public sealed class NumericSummaryDto
{
    public int Count { get; init; }
    public double Sum { get; init; }
    public double Mean { get; init; }
    public double Median { get; init; }
    public double Min { get; init; }
    public double Max { get; init; }
    public double Range { get; init; }
    public double Variance { get; init; }
    public double StandardDeviation { get; init; }
    public double CoefficientOfVariation { get; init; }
    public double Percentile05 { get; init; }
    public double Percentile25 { get; init; }
    public double Percentile75 { get; init; }
    public double Percentile95 { get; init; }
    public double Iqr { get; init; }
    public double Mad { get; init; }
    public double Skewness { get; init; }
    public double KurtosisExcess { get; init; }
    public double JarqueBera { get; init; }
    public ConfidenceIntervalDto MeanConfidence95 { get; init; } = new();
}

public sealed class ConfidenceIntervalDto
{
    public double Lower { get; init; }
    public double Upper { get; init; }
}

public sealed class DistributionDto
{
    public string Name { get; init; } = "";
    public double Entropy { get; init; }
    public List<DistributionItemDto> Items { get; init; } = [];
}

public sealed class DistributionItemDto
{
    public string Key { get; init; } = "";
    public double Value { get; init; }
    public double Share { get; init; }
}

public sealed class HistogramDto
{
    public string Name { get; init; } = "";
    public List<HistogramBucketDto> Buckets { get; init; } = [];
}

public sealed class HistogramBucketDto
{
    public double FromInclusive { get; init; }
    public double ToExclusive { get; init; }
    public int Count { get; init; }
}

public sealed class LinearTrendDto
{
    public double Slope { get; init; }
    public double Intercept { get; init; }
    public double RSquared { get; init; }
    public string Direction { get; init; } = "";
    public double StartValue { get; init; }
    public double EndValue { get; init; }
}

public sealed class CorrelationDto
{
    public string Name { get; init; } = "";
    public double Pearson { get; init; }
    public double Spearman { get; init; }
}

public sealed class OutlierSummaryDto
{
    public int DurationIqrOutliersCount { get; init; }
    public int DurationZScoreOutliersCount { get; init; }
    public int IssueIqrOutliersCount { get; init; }
    public int IssueZScoreOutliersCount { get; init; }
    public double DurationLowerFence { get; init; }
    public double DurationUpperFence { get; init; }
    public double IssueLowerFence { get; init; }
    public double IssueUpperFence { get; init; }
    public List<OutlierRunDto> Runs { get; init; } = [];
}

public sealed class OutlierRunDto
{
    public Guid RunId { get; init; }
    public string ComponentName { get; init; } = "";
    public DateTimeOffset CreatedAtUtc { get; init; }
    public long DurationMs { get; init; }
    public int IssuesTotal { get; init; }
    public bool DurationOutlier { get; init; }
    public bool IssueOutlier { get; init; }
}

public sealed class ConcentrationDto
{
    public double ComponentRunHhi { get; init; }
    public double ComponentIssueHhi { get; init; }
    public double IssueGiniAcrossRuns { get; init; }
    public double FileSizeGini { get; init; }
}

public sealed class ComponentStatsDto
{
    public string ComponentName { get; init; } = "";
    public int RunsCount { get; init; }
    public double AvgDurationMs { get; init; }
    public double MedianDurationMs { get; init; }
    public double AvgIssues { get; init; }
    public double ErrorRate { get; init; }
    public double FallbackRate { get; init; }
    public double ShareOfRuns { get; init; }
}

public sealed class ToolStatsDto
{
    public string ToolName { get; init; } = "";
    public int Calls { get; init; }
    public double SuccessRate { get; init; }
    public double FallbackRate { get; init; }
    public double AvgDurationMs { get; init; }
    public double MedianDurationMs { get; init; }
}

public sealed class TimelinePointDto
{
    public DateTimeOffset BucketStartUtc { get; init; }
    public int RunsCount { get; init; }
    public double AvgDurationMs { get; init; }
    public double AvgIssues { get; init; }
    public double FallbackRate { get; init; }
    public double GatewayFailureRate { get; init; }
    public double MovingAverageDurationMs { get; init; }
}

public sealed class RecentRunDto
{
    public Guid RunId { get; init; }
    public string ComponentName { get; init; } = "";
    public DateTimeOffset CreatedAtUtc { get; init; }
    public int FilesCount { get; init; }
    public long DurationMs { get; init; }
    public int IssuesTotal { get; init; }
    public string AggregationMode { get; init; } = "";
    public bool UsedAnyFallback { get; init; }
    public bool IsGatewayFailure { get; init; }
    public bool IsDurationOutlier { get; init; }
    public bool IsIssueOutlier { get; init; }
}

public sealed class RunDetailsDto
{
    public Guid RunId { get; init; }
    public DateTimeOffset CreatedAtUtc { get; init; }
    public string ComponentName { get; init; } = "";
    public int FilesCount { get; init; }
    public int TotalInputChars { get; init; }
    public int TotalInputLines { get; init; }
    public long DurationMs { get; init; }
    public string AggregationMode { get; init; } = "";
    public bool UsedAnyFallback { get; init; }
    public bool IsGatewayFailure { get; init; }
    public string? FailureReason { get; init; }
    public string Summary { get; init; } = "";
    public int IssuesInfoCount { get; init; }
    public int IssuesWarningCount { get; init; }
    public int IssuesErrorCount { get; init; }
    public int IssuesTotal { get; init; }
    public int SuggestionsTotal { get; init; }
    public string? RawRequestJson { get; init; }
    public string? RawReportJson { get; init; }
    public List<RunFileDetailsDto> Files { get; init; } = [];
    public List<RunToolDetailsDto> Tools { get; init; } = [];
}

public sealed class RunFileDetailsDto
{
    public string FileName { get; init; } = "";
    public int CharCount { get; init; }
    public int LineCount { get; init; }
    public bool ContainsTodo { get; init; }
    public bool ContainsFixme { get; init; }
    public bool ContainsPotentialSecret { get; init; }
    public bool TruncatedForLlm { get; init; }
    public bool UsedFallback { get; init; }
    public int InfoCount { get; init; }
    public int WarningCount { get; init; }
    public int ErrorCount { get; init; }
    public int SuggestionsCount { get; init; }
    public long? ReviewDurationMs { get; init; }
}

public sealed class RunToolDetailsDto
{
    public string ToolName { get; init; } = "";
    public int Sequence { get; init; }
    public string? Label { get; init; }
    public bool Succeeded { get; init; }
    public bool UsedFallback { get; init; }
    public long? DurationMs { get; init; }
    public string? Details { get; init; }
}
