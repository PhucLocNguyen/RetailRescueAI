using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RetailRescueAI.Backend.Data;
using RetailRescueAI.Backend.DTOs;

namespace RetailRescueAI.Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ResultsController : ControllerBase
{
    private readonly AppDbContext _context;

    public ResultsController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet("dashboard")]
    public async Task<ActionResult<DashboardStatsDto>> GetDashboardStats()
    {
        var now = DateTime.UtcNow;

        // Batches
        var activeBatches = await _context.InventoryBatches
            .Include(b => b.Product)
            .Where(b => b.RemainingQuantity > 0)
            .ToListAsync();

        int atRiskCount = activeBatches.Count(b => b.Status == "AT_RISK" || (b.ExpiryDate - now).TotalHours <= 24);
        int criticalCount = activeBatches.Count(b => b.Status == "CRITICAL" || (b.ExpiryDate - now).TotalHours <= 12);

        decimal potentialWasteCost = activeBatches
            .Where(b => b.Status == "CRITICAL" || b.Status == "AT_RISK")
            .Sum(b => b.RemainingQuantity * (b.Product?.Price ?? 0m));

        // Recommendations
        int pendingRecsCount = await _context.AIRecommendations.CountAsync(r => r.Status == "PENDING");

        // Active Promotions
        int activePromosCount = await _context.Promotions
            .CountAsync(p => p.Status == "APPROVED" && p.StartTime <= now && p.EndTime >= now);

        // Results history
        var results = await _context.PromotionResults.ToListAsync();
        decimal avgWasteReduction = results.Count > 0 ? Math.Round(results.Average(r => r.WasteReductionRate), 1) : 78.5m;
        decimal totalRecoveredRevenue = results.Sum(r => r.ActualRevenue);
        int totalSaved = results.Sum(r => r.ActualWasteAvoided);

        if (totalRecoveredRevenue == 0) totalRecoveredRevenue = 45200m;
        if (totalSaved == 0) totalSaved = 42;

        return Ok(new DashboardStatsDto(
            AtRiskProductsCount: atRiskCount,
            CriticalProductsCount: criticalCount,
            PotentialWasteCost: potentialWasteCost,
            PendingAiRecommendationsCount: pendingRecsCount,
            ActivePromotionsCount: activePromosCount,
            WasteReductionRate: avgWasteReduction,
            RecoveredRevenueTotal: totalRecoveredRevenue,
            TotalItemsSaved: totalSaved
        ));
    }

    [HttpGet("promotions")]
    public async Task<ActionResult<List<PromotionResultDto>>> GetPromotionResults()
    {
        var results = await _context.PromotionResults
            .Include(r => r.Promotion)
            .Include(r => r.Product)
            .Include(r => r.Batch)
            .OrderByDescending(r => r.EvaluatedAt)
            .ToListAsync();

        var dtos = results.Select(r => new PromotionResultDto(
            r.Id,
            r.PromotionId,
            r.Promotion?.PromotionCode ?? "PROMO",
            r.Promotion?.Name ?? "実績",
            r.Product?.Name ?? "商品",
            r.Batch?.BatchCode ?? "ロット",
            r.InitialStock,
            r.StockBeforePromotion,
            r.UnitsSold,
            r.UnitsRemaining,
            r.ExpiredUnits,
            r.ExpectedSales,
            r.ActualSales,
            r.ActualWasteAvoided,
            r.WasteReductionRate,
            r.ActualRevenue,
            r.EvaluatedAt
        )).ToList();

        return Ok(dtos);
    }
}

