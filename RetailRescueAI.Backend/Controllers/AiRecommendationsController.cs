using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RetailRescueAI.Backend.Data;
using RetailRescueAI.Backend.DTOs;
using RetailRescueAI.Backend.Models;
using RetailRescueAI.Backend.Services.AI.Agents;

namespace RetailRescueAI.Backend.Controllers;

[ApiController]
[Route("api/ai")]
public class AiRecommendationsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly OrchestratorAgent _orchestratorAgent;
    private readonly ILogger<AiRecommendationsController> _logger;

    public AiRecommendationsController(
        AppDbContext context,
        OrchestratorAgent orchestratorAgent,
        ILogger<AiRecommendationsController> logger)
    {
        _context = context;
        _orchestratorAgent = orchestratorAgent;
        _logger = logger;
    }

    [HttpGet("recommendations")]
    public async Task<ActionResult<List<AIRecommendationDto>>> GetRecommendations()
    {
        var recs = await _context.AIRecommendations
            .Include(r => r.TargetProduct)
            .Include(r => r.TargetBatch)
            .Include(r => r.Evidences)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();

        var dtos = recs.Select(r => new AIRecommendationDto(
            r.Id,
            r.RecommendationCode,
            r.TargetProductId,
            r.TargetProduct?.Name ?? "不明商品",
            r.TargetBatchId,
            r.TargetBatch?.BatchCode ?? "不明ロット",
            r.RecommendationType,
            r.RiskLevel,
            r.RecommendedAction,
            r.RecommendedDiscountPercent,
            r.RecommendedComboPrice,
            r.StartTime,
            r.EndTime,
            r.ExpectedSales,
            r.ExpectedWasteReduction,
            r.ExpectedRevenue,
            r.Reason,
            r.Status,
            r.CreatedAt,
            r.Evidences.Select(e => new AIEvidenceDto(e.EvidenceKey, e.EvidenceValue, e.Description)).ToList()
        )).ToList();

        return Ok(dtos);
    }

    [HttpPost("recommendations/{id}/approve")]
    public async Task<ActionResult> ApproveRecommendation(int id, [FromBody] ApproveRecommendationRequest? request)
    {
        var rec = await _context.AIRecommendations
            .Include(r => r.TargetProduct)
            .Include(r => r.TargetBatch)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (rec == null) return NotFound("AI提案が見つかりません。");

        var now = DateTime.UtcNow;
        rec.Status = "APPROVED";
        rec.ReviewedAt = now;
        rec.ReviewedBy = "佐藤 店長 (Manager)";

        // Convert AI recommendation into an APPROVED Promotion
        var promo = new Promotion
        {
            PromotionCode = $"PROMO-{now:yyyyMMddHHmmss}",
            Name = rec.RecommendedAction,
            PromotionType = rec.RecommendationType,
            Status = "APPROVED", // Now manager approved!
            TargetProductId = rec.TargetProductId,
            TargetBatchId = rec.TargetBatchId,
            DiscountPercent = rec.RecommendedDiscountPercent,
            ComboPrice = rec.RecommendedComboPrice,
            StartTime = rec.StartTime,
            EndTime = rec.EndTime,
            CreatedVia = "AI_AGENT",
            CreatedBy = "OrchestratorAgent",
            ApprovedBy = "佐藤 店長 (Manager)",
            ApprovedAt = now,
            AiReasoning = rec.Reason,
            CreatedAt = now,
            UpdatedAt = now
        };

        _context.Promotions.Add(promo);
        await _context.SaveChangesAsync();

        rec.CreatedPromotionId = promo.Id;
        await _context.SaveChangesAsync();

        return Ok(new
        {
            success = true,
            message = $"提案「{rec.RecommendedAction}」を承認しました。プロモーションがレジで有効化されました。",
            promotionId = promo.Id
        });
    }

    [HttpPost("recommendations/{id}/reject")]
    public async Task<ActionResult> RejectRecommendation(int id, [FromBody] RejectRecommendationRequest request)
    {
        var rec = await _context.AIRecommendations.FindAsync(id);
        if (rec == null) return NotFound("AI提案が見つかりません。");

        rec.Status = "REJECTED";
        rec.ReviewedAt = DateTime.UtcNow;
        rec.ReviewedBy = "佐藤 店長 (Manager)";

        await _context.SaveChangesAsync();
        return Ok(new { success = true, message = $"AI提案を却下しました。理由: {request.Reason}" });
    }

    [HttpPost("run")]
    public async Task<ActionResult> TriggerManualAiAnalysis()
    {
        _logger.LogInformation("Manager triggered manual AI analysis pipeline.");
        var created = await _orchestratorAgent.RunFullPipelineAsync();
        return Ok(new
        {
            success = true,
            message = "AI分析が正常に完了しました。",
            createdCount = created.Count
        });
    }
}

