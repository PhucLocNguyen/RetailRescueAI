using Microsoft.AspNetCore.Mvc;
using RetailRescueAI.Backend.DTOs;
using RetailRescueAI.Backend.Services.Interfaces;

namespace RetailRescueAI.Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PromotionsController : ControllerBase
{
    private readonly IPromotionService _promotionService;

    public PromotionsController(IPromotionService promotionService)
    {
        _promotionService = promotionService;
    }

    [HttpGet]
    public async Task<ActionResult<List<PromotionDto>>> GetPromotions([FromQuery] string? status, CancellationToken cancellationToken)
    {
        var dtos = await _promotionService.GetPromotionsAsync(status, cancellationToken);
        return Ok(dtos);
    }

    [HttpPost]
    public async Task<ActionResult<PromotionDto>> CreatePromotion([FromBody] CreatePromotionRequest request, CancellationToken cancellationToken)
    {
        var dto = await _promotionService.CreatePromotionAsync(request, cancellationToken);
        return Ok(dto);
    }

    [HttpPut("{id}/approve")]
    public async Task<ActionResult> ApprovePromotion(int id, CancellationToken cancellationToken)
    {
        var success = await _promotionService.ApprovePromotionAsync(id, cancellationToken);
        if (!success) return NotFound("プロモーションが見つかりません。");

        return Ok(new { message = "プロモーションを承認しました。レジで即時有効になります。" });
    }

    [HttpPut("{id}/reject")]
    public async Task<ActionResult> RejectPromotion(int id, [FromBody] RejectRecommendationRequest request, CancellationToken cancellationToken)
    {
        var success = await _promotionService.RejectPromotionAsync(id, request.Reason, cancellationToken);
        if (!success) return NotFound("プロモーションが見つかりません。");

        return Ok(new { message = "プロモーションを却下しました。" });
    }
}

