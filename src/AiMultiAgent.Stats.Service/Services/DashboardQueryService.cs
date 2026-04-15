using AiMultiAgent.Stats.Contracts.Dashboard;
using AiMultiAgent.Stats.Service.Data;
using Microsoft.EntityFrameworkCore;

namespace AiMultiAgent.Stats.Service.Services;

public sealed class DashboardQueryService(StatsDbContext db)
{
    private readonly StatsDbContext _db = db;

    public async Task<DashboardOverviewDto> GetOverviewAsync(int days, string? componentName, CancellationToken ct)
    {
        var from = DateTimeOffset.UtcNow.AddDays(-Math.Abs(days <= 0 ? 30 : days));

        var query = _db.Runs
            .AsNoTracking()
            .Include(x => x.Files)
            .Include(x => x.Tools)
            .Where(x => x.CreatedAtUtc >= from);

        if (!string.IsNullOrWhiteSpace(componentName))
        {
            query = query.Where(x => x.ComponentName == componentName);
        }

        var runs = await query
            .OrderBy(x => x.CreatedAtUtc)
            .ToListAsync(ct);

        var durationValues = runs.Select(x => (double)x.DurationMs).ToArray();
        var issueValues = runs.Select(x => (double)x.IssuesTotal).ToArray();
        var errorValues = runs.Select(x => (double)x.IssuesErrorCount).ToArray();
        var warningValues = runs.Select(x => (double)x.IssuesWarningCount).ToArray();
        var fileCountValues = runs.Select(x => (double)x.FilesCount).ToArray();
        var inputCharsValues = runs.Select(x => (double)x.TotalInputChars).ToArray();
        var fileSizeValues = runs.SelectMany(x => x.Files).Select(x => (double)x.CharCount).ToArray();

        var durationIqr = MathStatistics.IqrOutliers(durationValues);
        var durationZ = MathStatistics.ZScoreOutliers(durationValues);
        var issueIqr = MathStatistics.IqrOutliers(issueValues);
        var issueZ = MathStatistics.ZScoreOutliers(issueValues);

        var outlierRuns = new List<OutlierRunDto>();
        for (var i = 0; i < runs.Count; i++)
        {
            var isDurationOutlier = durationIqr.Indexes.Contains(i) || durationZ.Contains(i);
            var isIssueOutlier = issueIqr.Indexes.Contains(i) || issueZ.Contains(i);

            if (!isDurationOutlier && !isIssueOutlier)
            {
                continue;
            }

            outlierRuns.Add(new OutlierRunDto
            {
                RunId = runs[i].Id,
                ComponentName = runs[i].ComponentName,
                CreatedAtUtc = runs[i].CreatedAtUtc,
                DurationMs = runs[i].DurationMs,
                IssuesTotal = runs[i].IssuesTotal,
                DurationOutlier = isDurationOutlier,
                IssueOutlier = isIssueOutlier
            });
        }

        var timeline = BuildTimeline(runs);

        var topComponents = runs
            .GroupBy(x => string.IsNullOrWhiteSpace(x.ComponentName) ? "Unknown" : x.ComponentName)
            .Select(g => new ComponentStatsDto
            {
                ComponentName = g.Key,
                RunsCount = g.Count(),
                AvgDurationMs = g.Average(x => x.DurationMs),
                MedianDurationMs = MathStatistics.Summarize(g.Select(x => (double)x.DurationMs)).Median,
                AvgIssues = g.Average(x => x.IssuesTotal),
                ErrorRate = g.Count() == 0 ? 0 : g.Count(x => x.IssuesErrorCount > 0) / (double)g.Count(),
                FallbackRate = g.Count() == 0 ? 0 : g.Count(x => x.UsedAnyFallback) / (double)g.Count(),
                ShareOfRuns = runs.Count == 0 ? 0 : g.Count() / (double)runs.Count
            })
            .OrderByDescending(x => x.RunsCount)
            .ThenByDescending(x => x.AvgIssues)
            .Take(10)
            .ToList();

        var tools = runs
            .SelectMany(x => x.Tools)
            .GroupBy(x => x.ToolName)
            .Select(g => new ToolStatsDto
            {
                ToolName = g.Key,
                Calls = g.Count(),
                SuccessRate = g.Count() == 0 ? 0 : g.Count(x => x.Succeeded) / (double)g.Count(),
                FallbackRate = g.Count() == 0 ? 0 : g.Count(x => x.UsedFallback) / (double)g.Count(),
                AvgDurationMs = g.Any(x => x.DurationMs.HasValue) ? g.Where(x => x.DurationMs.HasValue).Average(x => x.DurationMs!.Value) : 0,
                MedianDurationMs = MathStatistics.Summarize(g.Where(x => x.DurationMs.HasValue).Select(x => (double)x.DurationMs!.Value)).Median
            })
            .OrderBy(x => x.ToolName)
            .ToList();

        var severityDistribution = MathStatistics.Distribution("severity", new Dictionary<string, double>
        {
            ["info"] = runs.Sum(x => x.IssuesInfoCount),
            ["warning"] = runs.Sum(x => x.IssuesWarningCount),
            ["error"] = runs.Sum(x => x.IssuesErrorCount)
        });

        var aggregationDistribution = MathStatistics.Distribution(
            "aggregationMode",
            runs.GroupBy(x => string.IsNullOrWhiteSpace(x.AggregationMode) ? "unknown" : x.AggregationMode)
                .ToDictionary(g => g.Key, g => (double)g.Count(), StringComparer.OrdinalIgnoreCase));

        var fallbackCount = runs.Count(x => x.UsedAnyFallback);
        var gatewayFailureCount = runs.Count(x => x.IsGatewayFailure);
        var successCount = runs.Count(x => !x.IsGatewayFailure);
        var llmAggregationCount = runs.Count(x => string.Equals(x.AggregationMode, "llm", StringComparison.OrdinalIgnoreCase));
        var warningRuns = runs.Count(x => x.IssuesWarningCount > 0);
        var errorRuns = runs.Count(x => x.IssuesErrorCount > 0);

        var durationSummary = MathStatistics.Summarize(durationValues);
        var issueSummary = MathStatistics.Summarize(issueValues);

        return new DashboardOverviewDto
        {
            GeneratedAtUtc = DateTimeOffset.UtcNow,
            WindowStartUtc = from,
            WindowEndUtc = runs.LastOrDefault()?.CreatedAtUtc,
            RunsCount = runs.Count,
            ComponentsCount = runs.Select(x => x.ComponentName).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
            Rates = new RateBlockDto
            {
                SuccessRate = Rate(successCount, runs.Count),
                FallbackRate = Rate(fallbackCount, runs.Count),
                GatewayFailureRate = Rate(gatewayFailureCount, runs.Count),
                LlmAggregationRate = Rate(llmAggregationCount, runs.Count),
                WarningRunRate = Rate(warningRuns, runs.Count),
                ErrorRunRate = Rate(errorRuns, runs.Count),
                FallbackRateConfidence95 = MathStatistics.WilsonConfidence95(fallbackCount, runs.Count)
            },
            DurationMs = durationSummary,
            TotalInputChars = MathStatistics.Summarize(inputCharsValues),
            FilesPerRun = MathStatistics.Summarize(fileCountValues),
            IssuesPerRun = issueSummary,
            ErrorsPerRun = MathStatistics.Summarize(errorValues),
            WarningsPerRun = MathStatistics.Summarize(warningValues),
            FileSizeChars = MathStatistics.Summarize(fileSizeValues),
            SeverityDistribution = severityDistribution,
            AggregationModeDistribution = aggregationDistribution,
            DurationHistogram = MathStatistics.Histogram("durationMs", durationValues),
            IssueHistogram = MathStatistics.Histogram("issuesTotal", issueValues),
            FileSizeHistogram = MathStatistics.Histogram("fileSizeChars", fileSizeValues),
            DurationTrend = MathStatistics.LinearTrend(durationValues),
            IssuesTrend = MathStatistics.LinearTrend(issueValues),
            Correlations =
            [
                new CorrelationDto
                {
                    Name = "duration~inputChars",
                    Pearson = MathStatistics.Pearson(durationValues, inputCharsValues),
                    Spearman = MathStatistics.Spearman(durationValues, inputCharsValues)
                },
                new CorrelationDto
                {
                    Name = "duration~filesCount",
                    Pearson = MathStatistics.Pearson(durationValues, fileCountValues),
                    Spearman = MathStatistics.Spearman(durationValues, fileCountValues)
                },
                new CorrelationDto
                {
                    Name = "issues~inputChars",
                    Pearson = MathStatistics.Pearson(issueValues, inputCharsValues),
                    Spearman = MathStatistics.Spearman(issueValues, inputCharsValues)
                },
                new CorrelationDto
                {
                    Name = "issues~duration",
                    Pearson = MathStatistics.Pearson(issueValues, durationValues),
                    Spearman = MathStatistics.Spearman(issueValues, durationValues)
                }
            ],
            Outliers = new OutlierSummaryDto
            {
                DurationIqrOutliersCount = durationIqr.Indexes.Count,
                DurationZScoreOutliersCount = durationZ.Count,
                IssueIqrOutliersCount = issueIqr.Indexes.Count,
                IssueZScoreOutliersCount = issueZ.Count,
                DurationLowerFence = durationIqr.LowerFence,
                DurationUpperFence = durationIqr.UpperFence,
                IssueLowerFence = issueIqr.LowerFence,
                IssueUpperFence = issueIqr.UpperFence,
                Runs = outlierRuns
                    .OrderByDescending(x => x.DurationOutlier)
                    .ThenByDescending(x => x.IssueOutlier)
                    .ThenByDescending(x => x.CreatedAtUtc)
                    .Take(20)
                    .ToList()
            },
            Concentration = new ConcentrationDto
            {
                ComponentRunHhi = MathStatistics.Hhi(topComponents.Select(x => (double)x.RunsCount)),
                ComponentIssueHhi = MathStatistics.Hhi(
                    runs.GroupBy(x => x.ComponentName)
                        .Select(g => (double)g.Sum(x => x.IssuesTotal))),
                IssueGiniAcrossRuns = MathStatistics.Gini(issueValues),
                FileSizeGini = MathStatistics.Gini(fileSizeValues)
            },
            TopComponents = topComponents,
            Tools = tools,
            Timeline = timeline,
            RecentRuns = BuildRecentRuns(runs, durationIqr.Indexes, durationZ, issueIqr.Indexes, issueZ)
        };
    }

    public async Task<List<RecentRunDto>> GetRunsAsync(int skip, int take, string? componentName, CancellationToken ct)
    {
        skip = Math.Max(0, skip);
        take = Math.Clamp(take, 1, 200);

        var baseQuery = _db.Runs.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(componentName))
        {
            baseQuery = baseQuery.Where(x => x.ComponentName == componentName);
        }

        return await baseQuery
            .OrderByDescending(x => x.CreatedAtUtc)
            .Skip(skip)
            .Take(take)
            .Select(x => new RecentRunDto
            {
                RunId = x.Id,
                ComponentName = x.ComponentName,
                CreatedAtUtc = x.CreatedAtUtc,
                FilesCount = x.FilesCount,
                DurationMs = x.DurationMs,
                IssuesTotal = x.IssuesTotal,
                AggregationMode = x.AggregationMode,
                UsedAnyFallback = x.UsedAnyFallback,
                IsGatewayFailure = x.IsGatewayFailure,
                IsDurationOutlier = false,
                IsIssueOutlier = false
            })
            .ToListAsync(ct);
    }

    public async Task<RunDetailsDto?> GetRunDetailsAsync(Guid id, CancellationToken ct)
    {
        var run = await _db.Runs
            .AsNoTracking()
            .Include(x => x.Files)
            .Include(x => x.Tools)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

        if (run is null)
        {
            return null;
        }

        return new RunDetailsDto
        {
            RunId = run.Id,
            CreatedAtUtc = run.CreatedAtUtc,
            ComponentName = run.ComponentName,
            FilesCount = run.FilesCount,
            TotalInputChars = run.TotalInputChars,
            TotalInputLines = run.TotalInputLines,
            DurationMs = run.DurationMs,
            AggregationMode = run.AggregationMode,
            UsedAnyFallback = run.UsedAnyFallback,
            IsGatewayFailure = run.IsGatewayFailure,
            FailureReason = run.FailureReason,
            Summary = run.Summary,
            IssuesInfoCount = run.IssuesInfoCount,
            IssuesWarningCount = run.IssuesWarningCount,
            IssuesErrorCount = run.IssuesErrorCount,
            IssuesTotal = run.IssuesTotal,
            SuggestionsTotal = run.SuggestionsTotal,
            RawRequestJson = run.RawRequestJson,
            RawReportJson = run.RawReportJson,
            Files = run.Files
                .OrderBy(x => x.FileName, StringComparer.OrdinalIgnoreCase)
                .Select(x => new RunFileDetailsDto
                {
                    FileName = x.FileName,
                    CharCount = x.CharCount,
                    LineCount = x.LineCount,
                    ContainsTodo = x.ContainsTodo,
                    ContainsFixme = x.ContainsFixme,
                    ContainsPotentialSecret = x.ContainsPotentialSecret,
                    TruncatedForLlm = x.TruncatedForLlm,
                    UsedFallback = x.UsedFallback,
                    InfoCount = x.IssueInfoCount,
                    WarningCount = x.IssueWarningCount,
                    ErrorCount = x.IssueErrorCount,
                    SuggestionsCount = x.SuggestionsCount,
                    ReviewDurationMs = x.ReviewDurationMs
                })
                .ToList(),
            Tools = run.Tools
                .OrderBy(x => x.ToolName)
                .ThenBy(x => x.Sequence)
                .Select(x => new RunToolDetailsDto
                {
                    ToolName = x.ToolName,
                    Sequence = x.Sequence,
                    Label = x.Label,
                    Succeeded = x.Succeeded,
                    UsedFallback = x.UsedFallback,
                    DurationMs = x.DurationMs,
                    Details = x.Details
                })
                .ToList()
        };
    }

    private static List<TimelinePointDto> BuildTimeline(List<StatsRunEntity> runs)
    {
        if (runs.Count == 0)
        {
            return [];
        }

        var grouped = runs
            .GroupBy(x => new DateTimeOffset(
                x.CreatedAtUtc.Year,
                x.CreatedAtUtc.Month,
                x.CreatedAtUtc.Day,
                0, 0, 0,
                TimeSpan.Zero))
            .OrderBy(x => x.Key)
            .ToList();

        var avgDurations = grouped.Select(g => g.Average(x => x.DurationMs)).ToList();
        var result = new List<TimelinePointDto>(grouped.Count);

        for (var i = 0; i < grouped.Count; i++)
        {
            var bucket = grouped[i];
            result.Add(new TimelinePointDto
            {
                BucketStartUtc = bucket.Key,
                RunsCount = bucket.Count(),
                AvgDurationMs = bucket.Average(x => x.DurationMs),
                AvgIssues = bucket.Average(x => x.IssuesTotal),
                FallbackRate = Rate(bucket.Count(x => x.UsedAnyFallback), bucket.Count()),
                GatewayFailureRate = Rate(bucket.Count(x => x.IsGatewayFailure), bucket.Count()),
                MovingAverageDurationMs = MathStatistics.MovingAverage(avgDurations.Select(x => (double)x).ToArray(), i, 3)
            });
        }

        return result;
    }

    private static List<RecentRunDto> BuildRecentRuns(
        List<StatsRunEntity> runs,
        HashSet<int> durationIqr,
        HashSet<int> durationZ,
        HashSet<int> issueIqr,
        HashSet<int> issueZ)
    {
        return runs
            .Select((run, index) => new RecentRunDto
            {
                RunId = run.Id,
                ComponentName = run.ComponentName,
                CreatedAtUtc = run.CreatedAtUtc,
                FilesCount = run.FilesCount,
                DurationMs = run.DurationMs,
                IssuesTotal = run.IssuesTotal,
                AggregationMode = run.AggregationMode,
                UsedAnyFallback = run.UsedAnyFallback,
                IsGatewayFailure = run.IsGatewayFailure,
                IsDurationOutlier = durationIqr.Contains(index) || durationZ.Contains(index),
                IsIssueOutlier = issueIqr.Contains(index) || issueZ.Contains(index)
            })
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(30)
            .ToList();
    }

    private static double Rate(int numerator, int denominator)
        => denominator <= 0 ? 0 : numerator / (double)denominator;
}
