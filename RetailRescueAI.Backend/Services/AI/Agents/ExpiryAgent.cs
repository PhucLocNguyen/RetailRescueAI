using RetailRescueAI.Backend.Models;

namespace RetailRescueAI.Backend.Services.AI.Agents;

public record ExpiryAnalysisResult(
    InventoryBatch Batch,
    double HoursUntilExpiry,
    string RiskLevel
);

public class ExpiryAgent
{
    private readonly ILogger<ExpiryAgent> _logger;

    public ExpiryAgent(ILogger<ExpiryAgent> logger)
    {
        _logger = logger;
    }

    public List<ExpiryAnalysisResult> AnalyzeBatches(List<InventoryBatch> batches, DateTime currentReferenceTime)
    {
        _logger.LogInformation("[ExpiryAgent] Analyzing {Count} active inventory batches for expiry risk...", batches.Count);

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

            results.Add(new ExpiryAnalysisResult(batch, hoursRemaining, riskLevel));
        }

        return results;
    }
}

