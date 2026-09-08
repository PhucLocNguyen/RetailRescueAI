using RetailRescueAI.Backend.Models;
using RetailRescueAI.Backend.Services.AI;

namespace RetailRescueAI.Backend.Services.AI.Agents;

public record PromotionProposal(
    InventoryBatch TargetBatch,
    string PromotionType,
    string RiskLevel,
    string ActionTitle,
    decimal? DiscountPercent,
    decimal? ComboPrice,
    DateTime StartTime,
    DateTime EndTime,
    int ExpectedSales,
    int ExpectedWasteReduction,
    decimal ExpectedRevenue,
    string Reason,
    Dictionary<string, string> EvidenceMap
);

public class PromotionAgent
{
    private readonly ILLMService _llmService;
    private readonly ILogger<PromotionAgent> _logger;

    public PromotionAgent(ILLMService llmService, ILogger<PromotionAgent> logger)
    {
        _llmService = llmService;
        _logger = logger;
    }

    public async Task<List<PromotionProposal>> GenerateProposalsAsync(
        List<SalesAnalysisResult> analysisResults,
        DateTime currentReferenceTime,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[PromotionAgent] Generating promotion proposals based on retail data...");

        var proposals = new List<PromotionProposal>();

        // Only propose for items with risk AT_RISK or CRITICAL and PotentialWasteUnits > 0
        var candidateBatches = analysisResults
            .Where(r => (r.FinalEvaluatedRiskLevel == "CRITICAL" || r.FinalEvaluatedRiskLevel == "AT_RISK") && r.PotentialWasteUnits > 0)
            .OrderByDescending(r => r.PotentialWasteFinancialLoss)
            .ToList();

        foreach (var candidate in candidateBatches)
        {
            var batch = candidate.Batch;
            var product = batch.Product!;

            // Determine discount %: 20% for AT_RISK, 30% for CRITICAL (subject to product max discount)
            decimal discount = candidate.FinalEvaluatedRiskLevel == "CRITICAL" ? 30.0m : 20.0m;
            if (discount > product.MaxDiscountPercent)
            {
                discount = product.MaxDiscountPercent;
            }

            // Proposed time window: Start now or next hour, end right before expiry
            var startTime = currentReferenceTime;
            var maxEndTime = batch.ExpiryDate.AddMinutes(-30); // 30 minutes before actual expiry
            if (maxEndTime <= startTime)
            {
                maxEndTime = batch.ExpiryDate;
            }

            // Expected impact
            int expectedSales = Math.Min(batch.RemainingQuantity, (int)(candidate.PotentialWasteUnits * 0.85m) + candidate.EstimatedNormalSalesUntilExpiry);
            int expectedWasteSaved = Math.Max(0, expectedSales - candidate.EstimatedNormalSalesUntilExpiry);
            decimal discountedPrice = product.Price * (1 - discount / 100.0m);
            decimal expectedRevenue = expectedSales * discountedPrice;

            var actionTitle = $"{product.Name} {discount:F0}% OFF（直前割）";
            var promoType = "DIRECT_DISCOUNT";

            // If chicken bento, provide rich retail prompt to LLM
            var prompt = $@"
店舗商品: {product.Name} (コード: {product.ProductCode})
現在庫数: {batch.RemainingQuantity}個 (賞味期限まで残り{candidate.HoursUntilExpiry:F1}時間)
日販平均: {candidate.AverageDailySales}個/日
通常予測販売数: {candidate.EstimatedNormalSalesUntilExpiry}個
潜在的廃棄リスク: {candidate.PotentialWasteUnits}個
提案割引率: {discount}%
上記データに基づき、店長が納得できるプロモーションの根拠（日本語）を簡潔にまとめてください。";

            var systemPrompt = "あなたは大手スーパーマーケット専属のAI小売最適化エージェント（RetailRescue AI）です。データに基づき、廃棄ロス防止のためのプロモーション根拠を論理的に解説してください。";

            var aiReason = await _llmService.GenerateTextAsync(systemPrompt, prompt, cancellationToken);

            var evidence = new Dictionary<string, string>
            {
                { "remaining_stock", $"{batch.RemainingQuantity} 個" },
                { "hours_until_expiry", $"{candidate.HoursUntilExpiry:F1} 時間" },
                { "average_daily_sales", $"{candidate.AverageDailySales} 個/日" },
                { "potential_waste_units", $"{candidate.PotentialWasteUnits} 個" },
                { "potential_waste_cost", $"¥{candidate.PotentialWasteFinancialLoss:N0}" },
                { "suggested_discount", $"{discount}% OFF" },
                { "expected_waste_reduction_rate", $"{Math.Round((decimal)expectedWasteSaved / Math.Max(1, candidate.PotentialWasteUnits) * 100)}%" }
            };

            proposals.Add(new PromotionProposal(
                TargetBatch: batch,
                PromotionType: promoType,
                RiskLevel: candidate.FinalEvaluatedRiskLevel,
                ActionTitle: actionTitle,
                DiscountPercent: discount,
                ComboPrice: null,
                StartTime: startTime,
                EndTime: maxEndTime,
                ExpectedSales: expectedSales,
                ExpectedWasteReduction: expectedWasteSaved,
                ExpectedRevenue: expectedRevenue,
                Reason: aiReason,
                EvidenceMap: evidence
            ));
        }

        return proposals;
    }
}

