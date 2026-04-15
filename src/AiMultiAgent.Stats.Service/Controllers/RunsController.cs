using AiMultiAgent.Stats.Service.Services;
using Microsoft.AspNetCore.Mvc;

namespace AiMultiAgent.Stats.Service.Controllers;

[ApiController]
[Route("api/runs")]
public sealed class RunsController(DashboardQueryService dashboardQueryService) : ControllerBase
{
    private readonly DashboardQueryService _dashboardQueryService = dashboardQueryService;

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50,
        [FromQuery] string? componentName = null,
        CancellationToken ct = default)
    {
        var result = await _dashboardQueryService.GetRunsAsync(skip, take, componentName, ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct = default)
    {
        var result = await _dashboardQueryService.GetRunDetailsAsync(id, ct);
        return result is null ? NotFound() : Ok(result);
    }
}
