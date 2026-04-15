using AiMultiAgent.Stats.Service.Services;
using Microsoft.AspNetCore.Mvc;

namespace AiMultiAgent.Stats.Service.Controllers;

[ApiController]
[Route("api/dashboard")]
public sealed class DashboardController(DashboardQueryService dashboardQueryService) : ControllerBase
{
    private readonly DashboardQueryService _dashboardQueryService = dashboardQueryService;

    [HttpGet("overview")]
    public async Task<IActionResult> Overview(
        [FromQuery] int days = 30,
        [FromQuery] string? componentName = null,
        CancellationToken ct = default)
    {
        var result = await _dashboardQueryService.GetOverviewAsync(days, componentName, ct);
        return Ok(result);
    }
}
