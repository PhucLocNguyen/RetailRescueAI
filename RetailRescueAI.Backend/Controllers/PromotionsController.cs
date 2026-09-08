using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RetailRescueAI.Backend.Data;
using RetailRescueAI.Backend.DTOs;
using RetailRescueAI.Backend.Models;

namespace RetailRescueAI.Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PromotionsController : ControllerBase
{
    private readonly AppDbContext _context;

    public PromotionsController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<List<PromotionDto>>> GetPromotions([FromQuery] string? status)
    {
        var query = _context.Promotions
            .Include(p => p.TargetProduct)
            .Include(p => p.TargetBatch)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(p => p.Status == status.ToUpper());
        }

        var promotions = await query.OrderByDescending(p => p.CreatedAt).ToListAsync();

        var dtos = promotions.Select(p => new PromotionDto(
            p.Id,
            p.PromotionCode,
            p.Name,
            p.PromotionType,
            p.Status,
            p.TargetProductId,
            p.TargetProduct?.Name,
            p.TargetBatch?.BatchCode,
            p.DiscountPercent,
            p.ComboPrice,
            p.StartTime,
            p.EndTime,
            p.CreatedVia,
            p.CreatedBy,
            p.ApprovedBy,
            p.ApprovedAt,
            p.AiReasoning,
            p.CreatedAt
        )).ToList();

        return Ok(dtos);
    }

    [HttpPost]
    public async Task<ActionResult<PromotionDto>> CreatePromotion([FromBody] CreatePromotionRequest request)
    {
        var now = DateTime.UtcNow;
        var promoCode = $"PROMO-{now:yyyyMMddHHmmss}";

        var promo = new Promotion
        {
            PromotionCode = promoCode,
            Name = request.Name,
            PromotionType = request.PromotionType,
            Status = "PENDING", // Always PENDING initially (BR-001)
            TargetProductId = request.TargetProductId,
            TargetBatchId = request.TargetBatchId,
            DiscountPercent = request.DiscountPercent,
            ComboPrice = request.ComboPrice,
            StartTime = request.StartTime,
            EndTime = request.EndTime,
            CreatedVia = "MANAGER",
            CreatedBy = "佐藤 店長",
            AiReasoning = request.Reasoning,
            CreatedAt = now,
            UpdatedAt = now
        };

        _context.Promotions.Add(promo);
        await _context.SaveChangesAsync();

        var created = await _context.Promotions
            .Include(p => p.TargetProduct)
            .Include(p => p.TargetBatch)
            .FirstAsync(p => p.Id == promo.Id);

        return Ok(new PromotionDto(
            created.Id,
            created.PromotionCode,
            created.Name,
            created.PromotionType,
            created.Status,
            created.TargetProductId,
            created.TargetProduct?.Name,
            created.TargetBatch?.BatchCode,
            created.DiscountPercent,
            created.ComboPrice,
            created.StartTime,
            created.EndTime,
            created.CreatedVia,
            created.CreatedBy,
            created.ApprovedBy,
            created.ApprovedAt,
            created.AiReasoning,
            created.CreatedAt
        ));
    }

    [HttpPut("{id}/approve")]
    public async Task<ActionResult> ApprovePromotion(int id)
    {
        var promo = await _context.Promotions.FindAsync(id);
        if (promo == null) return NotFound("プロモーションが見つかりません。");

        promo.Status = "APPROVED";
        promo.ApprovedBy = "佐藤 店長 (Manager)";
        promo.ApprovedAt = DateTime.UtcNow;
        promo.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return Ok(new { message = $"プロモーション {promo.Name} を承認しました。レジで即時有効になります。" });
    }

    [HttpPut("{id}/reject")]
    public async Task<ActionResult> RejectPromotion(int id, [FromBody] RejectRecommendationRequest request)
    {
        var promo = await _context.Promotions.FindAsync(id);
        if (promo == null) return NotFound("プロモーションが見つかりません。");

        promo.Status = "REJECTED";
        promo.RejectionReason = request.Reason;
        promo.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return Ok(new { message = $"プロモーション {promo.Name} を却下しました。" });
    }
}

