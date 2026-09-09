using RetailRescueAI.Backend.Models;
using RetailRescueAI.Backend.Repositories.Interfaces;

namespace RetailRescueAI.Backend.Services.AI.Agents;

public record SalesAnalysisResult(
    InventoryBatch Batch,
    double HoursUntilExpiry,
    string ExpiryRiskLevel,
    decimal AverageDailySales,
    int EstimatedNormalSalesUntilExpiry,
    int PotentialWasteUnits,
    decimal PotentialWasteFinancialLoss,
    string FinalEvaluatedRiskLevel // Escalates if velocity cannot clear stock
);

public class SalesAgent
{
    private readonly ISaleRepository _saleRepository;
    private readonly ILogger<SalesAgent> _logger;

    public SalesAgent(ISaleRepository saleRepository, ILogger<SalesAgent> logger)
    {
        _saleRepository = saleRepository;
        _logger = logger;
    }

    public async Task<List<SalesAnalysisResult>> AnalyzeSalesVelocityAsync(
        List<ExpiryAnalysisResult> expiryResults,
        DateTime currentReferenceTime,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[SalesAgent] Analyzing sales velocity and expected clearance...");

        var results = new List<SalesAnalysisResult>();

        // Analyze last 7 days of sales for each product
        var sevenDaysAgo = currentReferenceTime.AddDays(-7);
        var recentSaleItems = await _saleRepository.GetRecentSaleItemsAsync(sevenDaysAgo, cancellationToken);

        foreach (var exp in expiryResults)
        {
            var batch = exp.Batch;
            var product = batch.Product!;

            // Calculate product's sales in last 7 days
            var productSoldUnits = recentSaleItems
                .Where(si => si.ProductId == product.Id)
                .Sum(si => si.Quantity);

            // Average daily sales (default to 10 if new)
            decimal avgDailySales = productSoldUnits > 0 ? Math.Round((decimal)productSoldUnits / 7.0m, 1) : 10.0m;

            // Estimated normal sales until expiry
            var daysRemaining = (decimal)Math.Max(0, exp.HoursUntilExpiry) / 24.0m;
            var estimatedNormalSales = (int)Math.Floor(avgDailySales * daysRemaining);

            // Potential waste
            var potentialWasteUnits = Math.Max(0, batch.RemainingQuantity - estimatedNormalSales);
            var potentialWasteCost = potentialWasteUnits * product.Price;

            // Determine if risk escalates due to slow velocity
            string finalRisk = exp.RiskLevel;
            if (potentialWasteUnits >= 15 && exp.HoursUntilExpiry <= 24)
            {
                finalRisk = "CRITICAL";
            }
            else if (potentialWasteUnits > 0 && exp.HoursUntilExpiry <= 36)
            {
                if (finalRisk == "LOW" || finalRisk == "MEDIUM") finalRisk = "AT_RISK";
            }

            results.Add(new SalesAnalysisResult(
                Batch: batch,
                HoursUntilExpiry: exp.HoursUntilExpiry,
                ExpiryRiskLevel: exp.RiskLevel,
                AverageDailySales: avgDailySales,
                EstimatedNormalSalesUntilExpiry: estimatedNormalSales,
                PotentialWasteUnits: potentialWasteUnits,
                PotentialWasteFinancialLoss: potentialWasteCost,
                FinalEvaluatedRiskLevel: finalRisk
            ));
        }

        return results;
    }
}

