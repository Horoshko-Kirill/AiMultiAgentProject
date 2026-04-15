using System.Net.Mime;
using System.Text.Json;
using AiMultiAgent.Stats.Contracts.Pm;
using AiMultiAgent.Stats.Service.Services;
using Microsoft.AspNetCore.Mvc;

namespace AiMultiAgent.Stats.Service.Controllers;

[ApiController]
[Route("pmdebug/mcp")]
public sealed class PmProxyController(
    UpstreamPmClient upstreamPmClient,
    RunCaptureService runCaptureService,
    ILogger<PmProxyController> logger) : ControllerBase
{
    private readonly UpstreamPmClient _upstreamPmClient = upstreamPmClient;
    private readonly RunCaptureService _runCaptureService = runCaptureService;
    private readonly ILogger<PmProxyController> _logger = logger;

    [HttpPost("report")]
    [Produces(MediaTypeNames.Application.Json)]
    public async Task<IActionResult> Report([FromBody] PmRequestWire request, CancellationToken ct)
    {
        var upstream = await _upstreamPmClient.ProxyReportAsync(request, ct);

        Guid? runId = null;
        try
        {
            var projection = await _runCaptureService.CaptureAsync(request, upstream, ct);
            runId = projection.Run.Id;

            Response.Headers.Append("X-Stats-RunId", projection.Run.Id.ToString());
            Response.Headers.Append("X-Stats-DurationMs", projection.Run.DurationMs.ToString());
            Response.Headers.Append("X-Stats-AggregationMode", projection.Run.AggregationMode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Statistics capture failed, but proxy response will still be returned.");
        }

        if (!upstream.IsSuccess)
        {
            _logger.LogWarning(
                "Upstream PM call failed. RunId={RunId}, Status={StatusCode}, Reason={Reason}",
                runId,
                (int)upstream.StatusCode,
                upstream.FailureReason);

            return StatusCode((int)upstream.StatusCode, TryParseJson(upstream.ResponseBody));
        }

        return Content(upstream.ResponseBody, MediaTypeNames.Application.Json);
    }

    private static object TryParseJson(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            return doc.RootElement.Clone();
        }
        catch
        {
            return new { error = body };
        }
    }
}
