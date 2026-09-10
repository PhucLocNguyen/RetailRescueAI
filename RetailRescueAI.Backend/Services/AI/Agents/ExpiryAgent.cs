using Microsoft.SemanticKernel;
using RetailRescueAI.Backend.Models;
using RetailRescueAI.Backend.Services.AI.Plugins;

namespace RetailRescueAI.Backend.Services.AI.Agents;

public record ExpiryAnalysisResult(
    InventoryBatch Batch,
    double HoursUntilExpiry,
    string RiskLevel
);

/// <summary>
/// Semantic Kernel Agent responsible for scanning inventory batches,
/// evaluating remaining shelf-life, and tagging urgency levels.
/// </summary>
public class ExpiryAgent
{
    private readonly Kernel _kernel;
    private readonly InventoryDataPlugin _inventoryPlugin;
    private readonly ILogger<ExpiryAgent> _logger;

    public string Name => "ExpiryRiskAgent";
    public string RoleTitle => "賞味期限リスク監視エージェント (Semantic Kernel)";

    public ExpiryAgent(Kernel kernel, InventoryDataPlugin inventoryPlugin, ILogger<ExpiryAgent> logger)
    {
        _kernel = kernel;
        _inventoryPlugin = inventoryPlugin;
        _logger = logger;
    }

    public async Task<List<ExpiryAnalysisResult>> AnalyzeBatchesAsync(
        List<InventoryBatch> batches,
        DateTime currentReferenceTime,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[{Agent}] Analyzing {Count} active inventory batches for expiry risk via Semantic Kernel...", Name, batches.Count);

        var results = new List<ExpiryAnalysisResult>();

        foreach (var batch in batches)
        {
            if (batch.RemainingQuantity <= 0) continue;

            var hoursRemaining = (batch.ExpiryDate - currentReferenceTime).TotalHours;

            string riskLevel;
            if (hoursRemaining <= 0)
            {
                riskLevel = "EXPIRED";
            }
            else if (hoursRemaining <= 12)
            {
                riskLevel = "CRITICAL";
            }
            else if (hoursRemaining <= 24)
            {
                riskLevel = "AT_RISK";
            }
            else if (hoursRemaining <= 48)
            {
                riskLevel = "MEDIUM";
            }
            else
            {
                riskLevel = "LOW";
            }

            // If status changed, update via InventoryDataPlugin
            if (batch.Status != riskLevel)
            {
                await _inventoryPlugin.UpdateBatchStatusAsync(batch.Id, riskLevel, cancellationToken);
                batch.Status = riskLevel;
            }

            results.Add(new ExpiryAnalysisResult(batch, hoursRemaining, riskLevel));
        }

        return results;
    }
}
