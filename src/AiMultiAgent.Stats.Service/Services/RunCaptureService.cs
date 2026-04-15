using System.Text.Json;
using AiMultiAgent.Stats.Contracts.Pm;
using AiMultiAgent.Stats.Service.Data;
using AiMultiAgent.Stats.Service.Models;
using Microsoft.EntityFrameworkCore;

namespace AiMultiAgent.Stats.Service.Services;

public sealed class RunCaptureService(
    StatsDbContext db,
    PmReportProjector projector,
    IConfiguration configuration)
{
    private readonly StatsDbContext _db = db;
    private readonly PmReportProjector _projector = projector;
    private readonly IConfiguration _configuration = configuration;

    public async Task<RunProjection> CaptureAsync(
        PmRequestWire request,
        UpstreamPmClient.UpstreamPmResult upstream,
        CancellationToken ct)
    {
        var storeRawPayloads = bool.TryParse(
            Environment.GetEnvironmentVariable("STATS_STORE_RAW_PAYLOADS"),
            out var envValue)
            ? envValue
            : _configuration.GetValue("Stats:StoreRawPayloads", true);

        var rawRequest = JsonSerializer.Serialize(request);
        var rawReport = storeRawPayloads ? upstream.ResponseBody : null;

        RunProjection projection;

        if (upstream.IsSuccess)
        {
            projection = _projector.ProjectSuccess(
                request,
                storeRawPayloads ? rawRequest : "",
                rawReport ?? "",
                upstream.FinishedAtUtc,
                upstream.DurationMs,
                (int)upstream.StatusCode);
        }
        else
        {
            projection = _projector.ProjectFailure(
                request,
                storeRawPayloads ? rawRequest : "",
                rawReport,
                upstream.FinishedAtUtc,
                upstream.DurationMs,
                (int)upstream.StatusCode,
                upstream.FailureReason ?? $"Upstream status code {(int)upstream.StatusCode}");
        }

        await _db.Runs.AddAsync(projection.Run, ct);
        await _db.Files.AddRangeAsync(projection.Files, ct);
        await _db.Tools.AddRangeAsync(projection.Tools, ct);
        await _db.SaveChangesAsync(ct);

        return projection;
    }

    public Task<StatsRunEntity?> GetRunAsync(Guid id, CancellationToken ct)
        => _db.Runs
            .Include(x => x.Files)
            .Include(x => x.Tools)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
}
