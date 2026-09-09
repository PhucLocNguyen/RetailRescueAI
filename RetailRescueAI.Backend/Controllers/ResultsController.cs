using Microsoft.AspNetCore.Mvc;
using RetailRescueAI.Backend.DTOs;
using RetailRescueAI.Backend.Services.Interfaces;

namespace RetailRescueAI.Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ResultsController : ControllerBase
{
    private readonly IResultService _resultService;

    public ResultsController(IResultService resultService)
    {
        _resultService = resultService;
    }

    [HttpGet("dashboard")]
    public async Task<ActionResult<DashboardStatsDto>> GetDashboardStats(CancellationToken cancellationToken)
    {
        var stats = await _resultService.GetDashboardStatsAsync(cancellationToken);
        return Ok(stats);
    }

    [HttpGet("promotions")]
    public async Task<ActionResult<List<PromotionResultDto>>> GetPromotionResults(CancellationToken cancellationToken)
    {
        var results = await _resultService.GetPromotionResultsAsync(cancellationToken);
        return Ok(results);
    }
}

