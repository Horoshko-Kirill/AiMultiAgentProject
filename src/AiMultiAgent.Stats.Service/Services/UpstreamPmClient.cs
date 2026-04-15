using System.Net;
using System.Net.Http.Json;
using System.Text;
using AiMultiAgent.Stats.Contracts.Pm;

namespace AiMultiAgent.Stats.Service.Services;

public sealed class UpstreamPmClient(IHttpClientFactory httpClientFactory, IConfiguration configuration)
{
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly IConfiguration _configuration = configuration;

    public async Task<UpstreamPmResult> ProxyReportAsync(PmRequestWire request, CancellationToken ct)
    {
        var upstreamUrl =
            Environment.GetEnvironmentVariable("PM_UPSTREAM_REPORT_URL") ??
            _configuration["Stats:UpstreamReportUrl"] ??
            "http://api:7244/pmdebug/mcp/report";

        var client = _httpClientFactory.CreateClient(nameof(UpstreamPmClient));

        using var message = new HttpRequestMessage(HttpMethod.Post, upstreamUrl)
        {
            Content = JsonContent.Create(request)
        };

        var started = DateTimeOffset.UtcNow;
        try
        {
            using var response = await client.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, ct);
            var body = await response.Content.ReadAsStringAsync(ct);

            return new UpstreamPmResult
            {
                StartedAtUtc = started,
                FinishedAtUtc = DateTimeOffset.UtcNow,
                StatusCode = response.StatusCode,
                ResponseBody = body,
                IsNetworkFailure = false
            };
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return new UpstreamPmResult
            {
                StartedAtUtc = started,
                FinishedAtUtc = DateTimeOffset.UtcNow,
                StatusCode = HttpStatusCode.GatewayTimeout,
                ResponseBody = "{\"error\":\"upstream timeout\"}",
                IsNetworkFailure = true,
                FailureReason = "Upstream PM endpoint timed out."
            };
        }
        catch (Exception ex)
        {
            return new UpstreamPmResult
            {
                StartedAtUtc = started,
                FinishedAtUtc = DateTimeOffset.UtcNow,
                StatusCode = HttpStatusCode.BadGateway,
                ResponseBody = $"{{\"error\":\"{Escape(ex.Message)}\"}}",
                IsNetworkFailure = true,
                FailureReason = ex.Message
            };
        }
    }

    private static string Escape(string? s)
        => string.IsNullOrWhiteSpace(s)
            ? "unknown"
            : s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", " ");

    public sealed class UpstreamPmResult
    {
        public DateTimeOffset StartedAtUtc { get; init; }
        public DateTimeOffset FinishedAtUtc { get; init; }
        public HttpStatusCode StatusCode { get; init; }
        public string ResponseBody { get; init; } = "";
        public bool IsNetworkFailure { get; init; }
        public string? FailureReason { get; init; }
        public long DurationMs => Math.Max(0, (long)(FinishedAtUtc - StartedAtUtc).TotalMilliseconds);
        public bool IsSuccess => !IsNetworkFailure && (int)StatusCode is >= 200 and < 300;
    }
}
