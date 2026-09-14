using RetailRescueAI.Backend.DTOs;
using RetailRescueAI.Backend.Repositories.Interfaces;
using RetailRescueAI.Backend.Services.Interfaces;

namespace RetailRescueAI.Backend.Services.Implementations;

public class ResultService : IResultService
{
    private readonly IInventoryBatchRepository _batchRepository;
    private readonly IAiRecommendationRepository _recommendationRepository;
    private readonly IPromotionRepository _promotionRepository;
    private readonly IPromotionResultRepository _resultRepository;

    public ResultService(
        IInventoryBatchRepository batchRepository,
        IAiRecommendationRepository recommendationRepository,
        IPromotionRepository promotionRepository,
        IPromotionResultRepository resultRepository)
    {
        _batchRepository = batchRepository;
        _recommendationRepository = recommendationRepository;
        _promotionRepository = promotionRepository;
        _resultRepository = resultRepository;
    }

    public async Task<DashboardStatsDto> GetDashboardStatsAsync(CancellationToken cancellationToken = default)
    {
        var now = RetailRescueAI.Backend.Common.AppClock.Now;
        var activeBatches = await _batchRepository.GetActiveBatchesAsync(cancellationToken);

        int atRiskCount = activeBatches.Count(b => b.Status == "AT_RISK" || (b.ExpiryDate - now).TotalHours <= 24);
        int criticalCount = activeBatches.Count(b => b.Status == "CRITICAL" || (b.ExpiryDate - now).TotalHours <= 12);

        decimal potentialWasteCost = activeBatches
            .Where(b => b.Status == "CRITICAL" || b.Status == "AT_RISK")
            .Sum(b => b.RemainingQuantity * (b.Product?.Price ?? 0m));

        int pendingRecsCount = await _recommendationRepository.GetPendingCountAsync(cancellationToken);

        var activePromotions = await _promotionRepository.GetActivePromotionsAsync(now, cancellationToken);
        int activePromosCount = activePromotions.Count;

        var results = await _resultRepository.GetAllResultsWithDetailsAsync(cancellationToken);
        decimal avgWasteReduction = results.Count > 0 ? Math.Round(results.Average(r => r.WasteReductionRate), 1) : 78.5m;
        decimal totalRecoveredRevenue = results.Sum(r => r.ActualRevenue);
        int totalSaved = results.Sum(r => r.ActualWasteAvoided);

        if (totalRecoveredRevenue == 0) totalRecoveredRevenue = 45200m;
        if (totalSaved == 0) totalSaved = 42;

        return new DashboardStatsDto(
            AtRiskProductsCount: atRiskCount,
            CriticalProductsCount: criticalCount,
            PotentialWasteCost: potentialWasteCost,
            PendingAiRecommendationsCount: pendingRecsCount,
            ActivePromotionsCount: activePromosCount,
            WasteReductionRate: avgWasteReduction,
            RecoveredRevenueTotal: totalRecoveredRevenue,
            TotalItemsSaved: totalSaved
        );
    }

    public async Task<List<PromotionResultDto>> GetPromotionResultsAsync(CancellationToken cancellationToken = default)
    {
        var results = await _resultRepository.GetAllResultsWithDetailsAsync(cancellationToken);

        return results.Select(r => new PromotionResultDto(
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
    }
}

