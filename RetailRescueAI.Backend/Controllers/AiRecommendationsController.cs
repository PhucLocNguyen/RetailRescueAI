using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using RetailRescueAI.Backend.DTOs;
using RetailRescueAI.Backend.Services.Interfaces;

namespace RetailRescueAI.Backend.Controllers;

[ApiController]
[Route("api/ai")]
public class AiRecommendationsController : ControllerBase
{
    private readonly IAiRecommendationService _recommendationService;
    private readonly ILogger<AiRecommendationsController> _logger;

    public AiRecommendationsController(
        IAiRecommendationService recommendationService,
        ILogger<AiRecommendationsController> logger)
    {
        _recommendationService = recommendationService;
        _logger = logger;
    }

    [HttpGet]
    [HttpGet("recommendations")]
    public async Task<ActionResult<List<AIRecommendationDto>>> GetRecommendations(CancellationToken cancellationToken)
    {
        var recs = await _recommendationService.GetRecommendationsAsync(cancellationToken);
        return Ok(recs);
    }

    [HttpPost("{id}/approve")]
    [HttpPost("recommendations/{id}/approve")]
    public async Task<ActionResult> ApproveRecommendation(
        int id,
        [FromBody] ApproveRecommendationRequest? request,
        CancellationToken cancellationToken)
    {
        var (success, message, promoId) = await _recommendationService.ApproveRecommendationAsync(id, request, cancellationToken);
        if (!success) return BadRequest(new { success = false, message });

        return Ok(new { success = true, message, promotionId = promoId });
    }

    [HttpPost("{id}/reject")]
    [HttpPost("recommendations/{id}/reject")]
    public async Task<ActionResult> RejectRecommendation(
        int id,
        [FromBody] RejectRecommendationRequest request,
        CancellationToken cancellationToken)
    {
        var success = await _recommendationService.RejectRecommendationAsync(id, request, cancellationToken);
        if (!success) return NotFound("AI提案が見つかりません。");

        return Ok(new { success = true, message = $"AI提案を却下しました。理由: {request.Reason}" });
    }

    [HttpPost("run")]
    public async Task<ActionResult<AiPipelineRunResponse>> TriggerManualAiAnalysis(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Manager triggered manual AI analysis pipeline.");
            var response = await _recommendationService.RunManualPipelineAsync(cancellationToken);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to run manual AI analysis pipeline.");
            return StatusCode(500, new { message = ex.Message });
        }
    }

    [HttpGet("run-stream")]
    public async Task StreamManualAiAnalysis(CancellationToken cancellationToken)
    {
        Response.ContentType = "text/event-stream";
        Response.Headers.Append("Cache-Control", "no-cache");
        Response.Headers.Append("Connection", "keep-alive");

        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        try
        {
            _logger.LogInformation("Manager triggered streaming AI analysis pipeline.");
            await foreach (var streamEvent in _recommendationService.StreamManualPipelineAsync(cancellationToken))
            {
                var json = JsonSerializer.Serialize(streamEvent, jsonOptions);
                await Response.WriteAsync($"data: {json}\n\n", cancellationToken);
                await Response.Body.FlushAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("AI pipeline streaming was canceled by client.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed during streaming AI analysis pipeline.");
            var errJson = JsonSerializer.Serialize(new { eventType = "error", message = ex.Message }, jsonOptions);
            await Response.WriteAsync($"data: {errJson}\n\n", cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);
        }
    }
}
