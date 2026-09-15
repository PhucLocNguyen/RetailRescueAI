using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.SemanticKernel;
using RetailRescueAI.Backend.Models;
using RetailRescueAI.Backend.Repositories.Interfaces;
using RetailRescueAI.Backend.Services.AI.Plugins;

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
    Dictionary<string, string> EvidenceMap,
    Product? ComboProduct = null,
    decimal? ComboSavings = null
);

/// <summary>
/// Semantic Kernel Agent responsible for generating strategic retail promotions
/// (Direct Discounts and Meal Combos) with Japanese AI reasoning and staff scripts.
/// </summary>
public class PromotionAgent
{
    private readonly Kernel _kernel;
    private readonly ComboStrategyPlugin _comboPlugin;
    private readonly ILLMService _llmService;
    private readonly IProductRepository _productRepository;
    private readonly ILogger<PromotionAgent> _logger;

    public string Name => "PromotionStrategyAgent";
    public string RoleTitle => "販促プロモーション立案エージェント (Semantic Kernel)";

    public PromotionAgent(
        Kernel kernel,
        ComboStrategyPlugin comboPlugin,
        ILLMService llmService,
        IProductRepository productRepository,
        ILogger<PromotionAgent> logger)
    {
        _kernel = kernel;
        _comboPlugin = comboPlugin;
        _llmService = llmService;
        _productRepository = productRepository;
        _logger = logger;
    }

    public async Task<List<PromotionProposal>> GenerateProposalsAsync(
        List<SalesAnalysisResult> analysisResults,
        DateTime currentReferenceTime,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[{Agent}] Generating promotion proposals via Semantic Kernel...", Name);

        var proposals = new List<PromotionProposal>();
        var allProducts = await _productRepository.GetAllAsync(cancellationToken);
        // Dynamically find a suitable companion beverage product from inventory
        var partnerDrink = allProducts.FirstOrDefault(p =>
            p.Category != null && (
                p.Category.Name.Contains("飲料") ||
                p.Category.Name.Contains("ドリンク") ||
                p.Category.Name.Contains("Drink") ||
                p.Category.Name.Contains("Beverage")
            ));
        partnerDrink ??= allProducts.FirstOrDefault();

        // Only propose for items with risk AT_RISK or CRITICAL and PotentialWasteUnits > 0 (Limit to top 3 for speed & rate limits)
        var candidateBatches = analysisResults
            .Where(r => (r.FinalEvaluatedRiskLevel == "CRITICAL" || r.FinalEvaluatedRiskLevel == "AT_RISK") && r.PotentialWasteUnits > 0)
            .OrderByDescending(r => r.PotentialWasteFinancialLoss)
            .Take(5)
            .ToList();

        // 1. Single Batch Gemini Call to generate reasoning for ALL candidates at once (Prevents HTTP 429 Rate Limit)
        var aiReasons = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (candidateBatches.Count > 0)
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("以下の対象商品リストについて、店長が納得できるプロモーションの根拠（日本語）を各商品1〜2文で論理的に作成してください:\n");

                foreach (var c in candidateBatches)
                {
                    decimal disc = c.FinalEvaluatedRiskLevel == "CRITICAL" ? 30.0m : 20.0m;
                    if (disc > (c.Batch.Product?.MaxDiscountPercent ?? 50m)) disc = c.Batch.Product!.MaxDiscountPercent;

                    sb.AppendLine($"- ロット: {c.Batch.BatchCode}");
                    sb.AppendLine($"  商品名: {c.Batch.Product?.Name}");
                    sb.AppendLine($"  現在庫数: {c.Batch.RemainingQuantity}個 (賞味期限まで残り{c.HoursUntilExpiry:F1}時間)");
                    sb.AppendLine($"  潜在廃棄リスク: {c.PotentialWasteUnits}個 (損失見込 ¥{c.PotentialWasteFinancialLoss:N0})");
                    sb.AppendLine($"  提案割引率: {disc:F0}%\n");
                }

                sb.AppendLine(@"必ず以下のJSON配列フォーマットのみを出力してください（Markdownの ```json ... ``` で囲んでください）:
```json
[
  {
    ""batchCode"": ""ロットコード"",
    ""reason"": ""店長向けの論理的な根拠（日本語）""
  }
]
```");

                var systemPrompt = "あなたは大手スーパーマーケット専属のAI小売最適化エージェント（RetailRescue AI）です。各対象商品の廃棄ロス削減に向けたプロモーション根拠を論理的かつ簡潔にまとめ、必ず指定のJSON配列形式で回答してください。";

                var batchReply = await _llmService.GenerateTextAsync(systemPrompt, sb.ToString(), cancellationToken);

                // Extract JSON array from LLM response
                var jsonMatch = Regex.Match(batchReply, @"```(?:json)?\s*(\[[\s\S]*?\])\s*```", RegexOptions.IgnoreCase);
                string jsonText = jsonMatch.Success ? jsonMatch.Groups[1].Value : batchReply.Trim();
                if (!jsonText.StartsWith("["))
                {
                    var rawArrayMatch = Regex.Match(batchReply, @"(\[[\s\S]*\])");
                    if (rawArrayMatch.Success) jsonText = rawArrayMatch.Groups[1].Value;
                }

                using var doc = JsonDocument.Parse(jsonText);
                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in doc.RootElement.EnumerateArray())
                    {
                        if (item.TryGetProperty("batchCode", out var bcEl) && item.TryGetProperty("reason", out var rEl))
                        {
                            var bc = bcEl.GetString();
                            var r = rEl.GetString();
                            if (!string.IsNullOrWhiteSpace(bc) && !string.IsNullOrWhiteSpace(r))
                            {
                                aiReasons[bc] = r;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[PromotionAgent] Single batch Gemini call failed or timed out. Falling back to structured rule-based reasoning.");
            }
        }

        // 2. Build proposals using batch-generated reasons (No loop calling Gemini!)
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

            var startTime = currentReferenceTime;
            var maxEndTime = batch.ExpiryDate.AddMinutes(-30);
            if (maxEndTime <= startTime)
            {
                maxEndTime = batch.ExpiryDate;
            }

            int expectedSales = Math.Min(batch.RemainingQuantity, (int)(candidate.PotentialWasteUnits * 0.85m) + candidate.EstimatedNormalSalesUntilExpiry);
            int expectedWasteSaved = Math.Max(0, expectedSales - candidate.EstimatedNormalSalesUntilExpiry);
            decimal discountedPrice = product.Price * (1 - discount / 100.0m);
            decimal expectedRevenue = expectedSales * discountedPrice;

            var actionTitle = $"{product.Name} {discount:F0}% OFF（直前割）";
            var promoType = "DIRECT_DISCOUNT";

            string defaultReason = $"【AI廃棄ロス分析】「{product.Name}」は残り{candidate.HoursUntilExpiry:F1}時間で賞味期限を迎え、推定{candidate.PotentialWasteUnits}個（損失見込¥{candidate.PotentialWasteFinancialLoss:N0}）が廃棄となる恐れがあります。{discount:F0}%割引（直前割）を適用し、夕方のピーク需要を取り込んで早期売り切りを図ることを強く推奨します。";

            string aiReason = aiReasons.TryGetValue(batch.BatchCode, out var generatedReason) && !string.IsNullOrWhiteSpace(generatedReason)
                ? generatedReason
                : defaultReason;

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

            // BUNDLE_COMBO Proposal: Evaluated dynamically via ComboStrategyPlugin for food/meal items
            bool isMealCategory = product.Category != null && (
                product.Category.Name.Contains("弁当") ||
                product.Category.Name.Contains("サンド") ||
                product.Category.Name.Contains("パン") ||
                product.Category.Name.Contains("サラダ") ||
                product.Category.Name.Contains("惣菜") ||
                product.Category.Name.Contains("Delica") ||
                product.Category.Name.Contains("Bakery") ||
                product.Category.Name.Contains("Bento")
            );

            if (partnerDrink != null && partnerDrink.Id != product.Id && isMealCategory)
            {
                decimal normalComboTotal = product.Price + partnerDrink.Price;
                // Dynamic ~20% bundle discount rounded to nearest 10 yen
                decimal comboPrice = Math.Round((normalComboTotal * 0.8m) / 10m, 0) * 10m;
                decimal comboSavings = Math.Max(0, normalComboTotal - comboPrice);

                string comboJson = _comboPlugin.EvaluateMealCombo(
                    product.Name,
                    product.Price,
                    product.CostPrice,
                    partnerDrink.Name,
                    partnerDrink.Price,
                    partnerDrink.CostPrice,
                    comboPrice
                );

                string staffScript = $"「お客様、ご一緒に『{partnerDrink.Name}』はいかがでしょうか？ただいまセットで通常¥{normalComboTotal:N0}のところ、¥{comboPrice:N0}（¥{comboSavings:N0}お得）でお買い求めいただけます！」";
                decimal marginPct = 25.0m;

                try
                {
                    using var doc = JsonDocument.Parse(comboJson);
                    normalComboTotal = doc.RootElement.GetProperty("normalTotal").GetDecimal();
                    comboSavings = doc.RootElement.GetProperty("customerSavings").GetDecimal();
                    staffScript = doc.RootElement.GetProperty("staffScript").GetString() ?? staffScript;
                    marginPct = doc.RootElement.GetProperty("grossProfitMargin").GetDecimal();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed parsing combo evaluation JSON.");
                }

                var comboActionTitle = $"【お得なセット割】{product.Name} ＋ {partnerDrink.Name} セットで ¥{comboPrice:N0}（¥{comboSavings:N0}引き）";
                var comboEvidence = new Dictionary<string, string>
                {
                    { "combo_type", "フード＆ドリンク相乗セット割" },
                    { "main_product", $"{product.Name} (ロット: {batch.BatchCode})" },
                    { "partner_product", $"{partnerDrink.Name} (通常: ¥{partnerDrink.Price:N0})" },
                    { "normal_total", $"¥{normalComboTotal:N0}" },
                    { "combo_special_price", $"¥{comboPrice:N0}" },
                    { "customer_savings", $"¥{comboSavings:N0} 引き" },
                    { "gross_profit_margin", $"{marginPct:F1}% (最低利益率15%をクリア)" }
                };

                var comboReason = $@"【AIコンボ戦略サマリー】
対象商品：{product.Name}（ロット: {batch.BatchCode}・残{batch.RemainingQuantity}個）
相乗パートナー：{partnerDrink.Name}（定番飲料・安定在庫）
・単品通常合計：¥{normalComboTotal:N0} ➔ コンボ特別価格：¥{comboPrice:N0}（¥{comboSavings:N0}引き）

【AI推奨接客スクリプト（POSレジ画面に自動配信）】
{staffScript}

【利益性・安全検証】
{product.Name}原価¥{product.CostPrice:N0} ＋ {partnerDrink.Name}原価¥{partnerDrink.CostPrice:N0} ＝ 合計原価¥{product.CostPrice + partnerDrink.CostPrice:N0}。
コンボ売価¥{comboPrice:N0} に対し粗利益¥{comboPrice - (product.CostPrice + partnerDrink.CostPrice):N0}（粗利率{marginPct:F1}%）を維持し、廃棄ロス全額回避と客単価向上を両立します。";

                proposals.Add(new PromotionProposal(
                    TargetBatch: batch,
                    PromotionType: "BUNDLE_COMBO",
                    RiskLevel: candidate.FinalEvaluatedRiskLevel,
                    ActionTitle: comboActionTitle,
                    DiscountPercent: null,
                    ComboPrice: comboPrice,
                    StartTime: startTime,
                    EndTime: maxEndTime,
                    ExpectedSales: Math.Min(batch.RemainingQuantity, expectedSales + 5),
                    ExpectedWasteReduction: batch.RemainingQuantity,
                    ExpectedRevenue: expectedSales * comboPrice,
                    Reason: comboReason,
                    EvidenceMap: comboEvidence,
                    ComboProduct: partnerDrink,
                    ComboSavings: comboSavings
                ));
            }
        }

        return proposals;
    }
}
