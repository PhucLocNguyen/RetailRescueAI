using System.Text.Json;
using Microsoft.SemanticKernel;
using RetailRescueAI.Backend.Models;
using RetailRescueAI.Backend.Services.AI.Plugins;

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

/// <summary>
/// Semantic Kernel Agent responsible for calculating sales velocity,
/// clearance projections, and potential waste financial loss.
/// </summary>
public class SalesAgent
{
    private readonly Kernel _kernel;
    private readonly SalesVelocityPlugin _salesPlugin;
    private readonly ILogger<SalesAgent> _logger;

    public string Name => "SalesVelocityAgent";
    public string RoleTitle => "販売速度・廃棄予測エージェント (Semantic Kernel)";

    public SalesAgent(Kernel kernel, SalesVelocityPlugin salesPlugin, ILogger<SalesAgent> logger)
    {
        _kernel = kernel;
        _salesPlugin = salesPlugin;
        _logger = logger;
    }

    public async Task<List<SalesAnalysisResult>> AnalyzeSalesVelocityAsync(
        List<ExpiryAnalysisResult> expiryResults,
        DateTime currentReferenceTime,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[{Agent}] Analyzing sales velocity and clearance via Semantic Kernel...", Name);

        var results = new List<SalesAnalysisResult>();

        foreach (var exp in expiryResults)
        {
            var batch = exp.Batch;
            var product = batch.Product!;

            // Calculate daily velocity via SalesVelocityPlugin
            decimal avgDailySales = await _salesPlugin.CalculateDailySalesVelocityAsync(product.Id, 7, cancellationToken);

            // Forecast clearance and potential waste
            string forecastJson = _salesPlugin.ForecastClearanceAndWaste(
                batch.RemainingQuantity,
                avgDailySales,
                exp.HoursUntilExpiry,
                product.Price
            );

            int estimatedNormalSales = 0;
            int potentialWasteUnits = 0;
            decimal potentialWasteCost = 0m;

            try
            {
                using var doc = JsonDocument.Parse(forecastJson);
                estimatedNormalSales = doc.RootElement.GetProperty("estimatedNormalSales").GetInt32();
                potentialWasteUnits = doc.RootElement.GetProperty("potentialWasteUnits").GetInt32();
                potentialWasteCost = doc.RootElement.GetProperty("potentialWasteFinancialLoss").GetDecimal();
            }
            catch
            {
                var daysRemaining = (decimal)Math.Max(0, exp.HoursUntilExpiry) / 24.0m;
                estimatedNormalSales = (int)Math.Floor(avgDailySales * daysRemaining);
                potentialWasteUnits = Math.Max(0, batch.RemainingQuantity - estimatedNormalSales);
                potentialWasteCost = potentialWasteUnits * product.Price;
            }

            // Determine if risk escalates due to slow velocity
            string finalRisk = exp.RiskLevel;
            if (potentialWasteUnits >= 15 && exp.HoursUntilExpiry <= 24)
            {
                finalRisk = "CRITICAL";
            }
            else if (potentialWasteUnits > 0 && exp.RiskLevel == "MEDIUM" && exp.HoursUntilExpiry <= 36)
            {
                finalRisk = "AT_RISK";
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
