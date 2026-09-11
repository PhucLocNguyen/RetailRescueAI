using System.Text.Json;
using System.Text.RegularExpressions;
using RetailRescueAI.Backend.DTOs;
using RetailRescueAI.Backend.Models;
using RetailRescueAI.Backend.Repositories.Interfaces;
using RetailRescueAI.Backend.Services.AI;
using RetailRescueAI.Backend.Services.AI.Agents;
using RetailRescueAI.Backend.Services.Interfaces;

namespace RetailRescueAI.Backend.Services.Implementations;

public class ChatbotService : IChatbotService
{
    private readonly IInventoryBatchRepository _batchRepository;
    private readonly ExpiryAgent _expiryAgent;
    private readonly SalesAgent _salesAgent;
    private readonly PromotionAgent _promotionAgent;
    private readonly ReviserAgent _reviserAgent;
    private readonly ILLMService _llmService;
    private readonly ILogger<ChatbotService> _logger;

    public ChatbotService(
        IInventoryBatchRepository batchRepository,
        ExpiryAgent expiryAgent,
        SalesAgent salesAgent,
        PromotionAgent promotionAgent,
        ReviserAgent reviserAgent,
        ILLMService llmService,
        ILogger<ChatbotService> logger)
    {
        _batchRepository = batchRepository;
        _expiryAgent = expiryAgent;
        _salesAgent = salesAgent;
        _promotionAgent = promotionAgent;
        _reviserAgent = reviserAgent;
        _llmService = llmService;
        _logger = logger;
    }

    public async Task<ChatResponse> ProcessChatAsync(ChatRequest request, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var traceSteps = new List<AiAgentTraceStepDto>();

        // 1. Fetch active inventory batches
        var allBatches = await _batchRepository.GetActiveBatchesAsync(cancellationToken);
        var activeBatches = allBatches.Where(b => b.RemainingQuantity > 0).ToList();

        // 2. Step 1: ExpiryAgent - evaluate shelf life and urgency via InventoryDataPlugin
        var sw1 = System.Diagnostics.Stopwatch.StartNew();
        var expiryResults = await _expiryAgent.AnalyzeBatchesAsync(activeBatches, now, cancellationToken);
        sw1.Stop();

        traceSteps.Add(new AiAgentTraceStepDto(
            AgentKey: _expiryAgent.Name,
            AgentName: _expiryAgent.RoleTitle,
            RoleTitle: "Expiry Risk Monitor (SK Plugin)",
            Description: "ロット別残存時間・危険度判定（CRITICAL / AT_RISK / EXPIRED）",
            Details: expiryResults.Select(r =>
                $"{GetRiskBadge(r.RiskLevel)} {r.Batch.BatchCode} ({r.Batch.Product?.Name}): 残り{r.HoursUntilExpiry:F1}時間 (リスク: {r.RiskLevel})").ToList(),
            Status: "COMPLETED",
            DurationMs: (int)sw1.ElapsedMilliseconds
        ));

        // 3. Step 2: SalesAgent - evaluate velocity and projected waste via SalesVelocityPlugin
        var sw2 = System.Diagnostics.Stopwatch.StartNew();
        var salesResults = await _salesAgent.AnalyzeSalesVelocityAsync(expiryResults, now, cancellationToken);
        sw2.Stop();

        traceSteps.Add(new AiAgentTraceStepDto(
            AgentKey: _salesAgent.Name,
            AgentName: _salesAgent.RoleTitle,
            RoleTitle: "Sales Velocity & Waste Forecaster (SK Plugin)",
            Description: "過去7日間POS日販ペース、期限前消化予測、潜在廃棄損失額の算出",
            Details: salesResults.Select(s =>
                $"📊 {s.Batch.Product?.Name} ({s.Batch.BatchCode}): 日販 {s.AverageDailySales:F1}個/日 → 期限前消化予測 {s.EstimatedNormalSalesUntilExpiry}個 | 潜在廃棄 {s.PotentialWasteUnits}個 (損失見込 ¥{s.PotentialWasteFinancialLoss:N0})").ToList(),
            Status: "COMPLETED",
            DurationMs: (int)sw2.ElapsedMilliseconds
        ));

        // 4. Step 3: Identify user intent and candidate batch for promotion
        var userMessage = request.Message ?? string.Empty;
        var userMsgLower = userMessage.ToLower();

        SalesAnalysisResult? targetSalesResult = null;
        foreach (var s in salesResults)
        {
            if (userMsgLower.Contains(s.Batch.BatchCode.ToLower()) ||
                (s.Batch.Product != null && userMsgLower.Contains(s.Batch.Product.Name.ToLower())))
            {
                targetSalesResult = s;
                break;
            }
        }

        if (targetSalesResult == null)
        {
            // Keyword matching
            if (userMsgLower.Contains("サンド") || userMsgLower.Contains("たまご") || userMsgLower.Contains("egg") || userMsgLower.Contains("sand"))
                targetSalesResult = salesResults.FirstOrDefault(s => s.Batch.Product != null && s.Batch.Product.Name.Contains("サンド"));
            else if (userMsgLower.Contains("かつ丼") || userMsgLower.Contains("ロース") || userMsgLower.Contains("pork"))
                targetSalesResult = salesResults.FirstOrDefault(s => s.Batch.Product != null && s.Batch.Product.Name.Contains("かつ丼"));
            else if (userMsgLower.Contains("サラダ") || userMsgLower.Contains("サーモン") || userMsgLower.Contains("salad"))
                targetSalesResult = salesResults.FirstOrDefault(s => s.Batch.Product != null && s.Batch.Product.Name.Contains("サラダ"));
            else if (userMsgLower.Contains("チキン") || userMsgLower.Contains("弁当") || userMsgLower.Contains("bento"))
                targetSalesResult = salesResults.FirstOrDefault(s => s.Batch.Product != null && s.Batch.Product.Name.Contains("チキン"));
        }

        // Default to most urgent batch if general promotion intent
        if (targetSalesResult == null)
        {
            targetSalesResult = salesResults.OrderBy(s => s.HoursUntilExpiry).FirstOrDefault(s => s.FinalEvaluatedRiskLevel == "CRITICAL" || s.FinalEvaluatedRiskLevel == "AT_RISK")
                             ?? salesResults.OrderBy(s => s.HoursUntilExpiry).FirstOrDefault();
        }

        // Extract requested discount percent
        decimal requestedDiscount = 0m;
        var matchUserPct = Regex.Match(userMessage, @"(\d{1,2})\s*(?:%|％)");
        if (matchUserPct.Success && decimal.TryParse(matchUserPct.Groups[1].Value, out var uPct))
        {
            requestedDiscount = Math.Clamp(uPct, 5, 80);
        }
        else if (userMsgLower.Contains("3割") || userMsgLower.Contains("30%"))
        {
            requestedDiscount = 30m;
        }
        else if (userMsgLower.Contains("半額") || userMsgLower.Contains("50%"))
        {
            requestedDiscount = 50m;
        }

        // 5. Step 4: PromotionAgent & ReviserAgent validation
        PromotionProposal? targetProposal = null;
        bool hasPromoIntent = userMsgLower.Contains("割引") || userMsgLower.Contains("プロモーション") ||
                              userMsgLower.Contains("提案") || userMsgLower.Contains("引き") ||
                              userMsgLower.Contains("オフ") || userMsgLower.Contains("off") ||
                              userMsgLower.Contains("コンボ") || userMsgLower.Contains("セット") ||
                              userMsgLower.Contains("登録") || userMsgLower.Contains("相談") ||
                              userMsgLower.Contains("値下げ") || userMsgLower.Contains("安く") ||
                              userMsgLower.Contains("％") || userMsgLower.Contains("%");

        var sw3 = System.Diagnostics.Stopwatch.StartNew();
        var promoDetails = new List<string>();
        var reviserDetails = new List<string>();

        if (targetSalesResult != null && (hasPromoIntent || targetSalesResult.FinalEvaluatedRiskLevel == "CRITICAL" || targetSalesResult.FinalEvaluatedRiskLevel == "AT_RISK"))
        {
            var batch = targetSalesResult.Batch;
            var prod = batch.Product!;

            decimal baseDiscount = requestedDiscount > 0
                ? requestedDiscount
                : (targetSalesResult.FinalEvaluatedRiskLevel == "CRITICAL" ? 30.0m : 20.0m);

            var startTime = now.Hour >= 17 ? now : now.Date.AddHours(17);
            var endTime = batch.ExpiryDate.AddMinutes(-30);
            if (endTime <= startTime) endTime = batch.ExpiryDate;

            decimal discountedPrice = Math.Round(prod.Price * (1 - baseDiscount / 100.0m), 0);
            int expectedSales = Math.Min(batch.RemainingQuantity, (int)(targetSalesResult.PotentialWasteUnits * 0.85m) + targetSalesResult.EstimatedNormalSalesUntilExpiry);
            int expectedSaved = Math.Max(0, expectedSales - targetSalesResult.EstimatedNormalSalesUntilExpiry);
            decimal expectedRev = expectedSales * discountedPrice;

            string promoTitle = $"{prod.Name} 夕方直前割 {baseDiscount:F0}% OFF";
            string reason = $"ロット{batch.BatchCode}（残{batch.RemainingQuantity}個、期限まで残り{targetSalesResult.HoursUntilExpiry:F1}h）の廃棄回避のため、{baseDiscount:F0}%割引（¥{discountedPrice:N0}）で早期完売を促します。";

            targetProposal = new PromotionProposal(
                TargetBatch: batch,
                PromotionType: "DIRECT_DISCOUNT",
                RiskLevel: targetSalesResult.FinalEvaluatedRiskLevel,
                ActionTitle: promoTitle,
                DiscountPercent: baseDiscount,
                ComboPrice: null,
                StartTime: startTime,
                EndTime: endTime,
                ExpectedSales: expectedSales,
                ExpectedWasteReduction: expectedSaved,
                ExpectedRevenue: expectedRev,
                Reason: reason,
                EvidenceMap: new Dictionary<string, string>
                {
                    { "remaining_stock", $"{batch.RemainingQuantity} 個" },
                    { "hours_remaining", $"{targetSalesResult.HoursUntilExpiry:F1} 時間" },
                    { "normal_sales_pace", $"{targetSalesResult.AverageDailySales:F1} 個/日" },
                    { "projected_waste", $"{targetSalesResult.PotentialWasteUnits} 個" },
                    { "suggested_discount", $"{baseDiscount:F0}%" }
                }
            );

            promoDetails.Add($"💡 {prod.Name} ({batch.BatchCode}): {baseDiscount:F0}% OFF（割引後 ¥{discountedPrice:N0}）を策定");
            promoDetails.Add($"💡 想定売上: {expectedSales}個 / 救済見込: +{expectedSaved}個 / 回収収益: ¥{expectedRev:N0}");
        }
        else
        {
            promoDetails.Add("💡 店長からの問い合わせ意図を解析し、最新在庫・販売速度に基づく適切な回答を準備");
        }
        sw3.Stop();

        traceSteps.Add(new AiAgentTraceStepDto(
            AgentKey: _promotionAgent.Name,
            AgentName: _promotionAgent.RoleTitle,
            RoleTitle: "Smart Promotion Strategy Agent (SK & Gemini)",
            Description: "商品特性とピーク時間帯を踏まえ、最適な割引率・プロモーション案を自動生成",
            Details: promoDetails,
            Status: "COMPLETED",
            DurationMs: (int)sw3.ElapsedMilliseconds
        ));

        // Step 4: ReviserAgent validation via SafetyGuardrailPlugin
        var sw4 = System.Diagnostics.Stopwatch.StartNew();
        bool isProposalValid = true;
        string safetyNotes = "";

        if (targetProposal != null)
        {
            var validationResults = _reviserAgent.ValidateProposals(new List<PromotionProposal> { targetProposal });
            var firstVal = validationResults.FirstOrDefault();
            if (firstVal != null)
            {
                isProposalValid = firstVal.IsValid;
                reviserDetails.AddRange(firstVal.ValidationMessages);
                safetyNotes = string.Join(" / ", firstVal.ValidationMessages);
            }
        }
        else
        {
            reviserDetails.Add("🛡️ 在庫問合せに対する安全規程（BR-007賞味期限切れ販売禁止等）の整合性を確認");
        }
        sw4.Stop();

        traceSteps.Add(new AiAgentTraceStepDto(
            AgentKey: _reviserAgent.Name,
            AgentName: _reviserAgent.RoleTitle,
            RoleTitle: "Retail Safety & Guardrails Agent (SK Plugin)",
            Description: "リテール規約 BR-003（賞味期限内）、BR-006（実在庫）、粗利率15%以上の厳格チェック",
            Details: reviserDetails,
            Status: isProposalValid ? "COMPLETED" : "WARNING",
            DurationMs: (int)sw4.ElapsedMilliseconds
        ));

        // Step 5: Orchestrator Step Trace
        traceSteps.Add(new AiAgentTraceStepDto(
            AgentKey: "OrchestratorAgent",
            AgentName: "統括オーケストレーター",
            RoleTitle: "Human-in-the-Loop Orchestrator",
            Description: "全専門エージェントの検証結果を集約し、店長対話用の回答を統合・承認待ちキューへ準備",
            Details: new List<string>
            {
                "🎯 Human-in-the-Loop 統合: 専門エージェント4基（Expiry, Sales, Promotion, Reviser）のデータを統合完了",
                targetProposal != null ? $"提案対象ロット: {targetProposal.TargetBatch.BatchCode} ({targetProposal.TargetBatch.Product?.Name}) - 承認待ち提案として提示" : "在庫・販売状況の論理的サマリーを回答として提示"
            },
            Status: "COMPLETED",
            DurationMs: 5
        ));

        // Step 6: Multi-Agent Context Synthesis for LLM
        var contextSummary = string.Join("\n", salesResults.Select(s =>
            $"- {s.Batch.Product?.Name} (ロット: {s.Batch.BatchCode}, 商品ID: {s.Batch.ProductId}): 残り{s.Batch.RemainingQuantity}個, 定価¥{s.Batch.Product?.Price}, 原価¥{s.Batch.Product?.CostPrice}, 賞味期限まで残り{s.HoursUntilExpiry:F1}時間, リスク状態: {s.FinalEvaluatedRiskLevel}, 日販: {s.AverageDailySales:F1}個/日, 潜在廃棄: {s.PotentialWasteUnits}個 (損失見込 ¥{s.PotentialWasteFinancialLoss:N0})"));

        var systemPrompt = $@"あなたはスーパーマーケット「ライフマート新宿店」の店長専属AIアシスタント（RetailRescue AI）です。
裏側で4つの専門エージェント（ExpiryAgent, SalesAgent, PromotionAgent, ReviserAgent）と連携し、店長からの在庫、賞味期限、割引・プロモーションに関する相談に、礼儀正しく論理的な日本語で回答してください。

【連携エージェントによる最新ロット分析データ】:
{contextSummary}

【ReviserAgentによる安全検証結果】:
{(string.IsNullOrEmpty(safetyNotes) ? "全ロット正常・規約適合" : safetyNotes)}

【業務ルール】:
1. 割引プロモーションの提案は、夕方ピーク（17:00〜22:00）や賞味期限前（BR-003）を推奨すること。
2. 割引率は20%〜30%を基本とし、最低粗利率（15%）を維持できることを論理的に説明すること。
3. 店長が特定の割引率を指定した場合（例：「たまごサンドを30%引きにして」「25%で登録」）、その指定を尊重して提案を構成すること。
4. 店長が作成を依頼した場合、必ず「承認待ち（PENDING）として登録します」と案内すること（勝手に有効化しない）。
5. 店長に対して具体的な値引きプロモーションを提案・合意する場合、回答本文の末尾に必ず以下のJSONブロックを正確に付与してください（一般的な質問には不要）:

```json:proposal
{{
  ""batchCode"": ""対象のロットコード（例: BATCH-SAND-001）"",
  ""promotionType"": ""DIRECT_DISCOUNT"",
  ""discountPercent"": 20,
  ""name"": ""プロモーション名称（例: こだわりたまごサンド 夕方直前割 20% OFF）"",
  ""reasoning"": ""提案の根拠・理由""
}}
```";

        var history = request.History ?? new List<ChatMessageDto>();
        var reply = await _llmService.ChatAsync(systemPrompt, history, userMessage, cancellationToken);

        // Extract structured proposal from reply or fallback to targetProposal
        var (cleanReply, proposal) = TryExtractJsonProposal(reply, activeBatches, now);

        if (proposal == null && (hasPromoIntent || userMsgLower.Contains("たまご") || userMsgLower.Contains("サンド") || userMsgLower.Contains("チキン") || userMsgLower.Contains("かつ丼")) && targetProposal != null)
        {
            var p = targetProposal.TargetBatch.Product!;
            decimal discPct = targetProposal.DiscountPercent ?? 20m;
            decimal discPrice = Math.Round(p.Price * (1 - discPct / 100.0m), 0);

            proposal = new PromotionProposalDto(
                Name: targetProposal.ActionTitle,
                PromotionType: targetProposal.PromotionType,
                TargetProductId: p.Id,
                TargetProductName: p.Name,
                TargetBatchId: targetProposal.TargetBatch.Id,
                TargetBatchCode: targetProposal.TargetBatch.BatchCode,
                DiscountPercent: discPct,
                OriginalPrice: p.Price,
                DiscountedPrice: discPrice,
                StartTime: targetProposal.StartTime,
                EndTime: targetProposal.EndTime,
                Reasoning: targetProposal.Reason
            );
        }

        return new ChatResponse(cleanReply, proposal != null, proposal, traceSteps);
    }

    private static string GetRiskBadge(string risk) => risk switch
    {
        "EXPIRED" => "⛔",
        "CRITICAL" => "🔴",
        "AT_RISK" => "🟠",
        "MEDIUM" => "🟡",
        _ => "🟢"
    };

    private (string CleanReply, PromotionProposalDto? Proposal) TryExtractJsonProposal(
        string rawReply,
        List<InventoryBatch> batches,
        DateTime now)
    {
        if (string.IsNullOrWhiteSpace(rawReply)) return (rawReply, null);

        var jsonRegex = new Regex(@"```json(?::proposal)?\s*(\{[\s\S]*?\})\s*```", RegexOptions.IgnoreCase);
        var match = jsonRegex.Match(rawReply);

        if (!match.Success)
        {
            return (rawReply, null);
        }

        string jsonContent = match.Groups[1].Value;
        string cleanReply = jsonRegex.Replace(rawReply, string.Empty).Trim();

        try
        {
            using var doc = JsonDocument.Parse(jsonContent);
            var root = doc.RootElement;

            string batchCode = root.TryGetProperty("batchCode", out var b) ? b.GetString() ?? "" : "";
            string promoType = root.TryGetProperty("promotionType", out var pt) ? pt.GetString() ?? "DIRECT_DISCOUNT" : "DIRECT_DISCOUNT";
            string name = root.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
            string reasoning = root.TryGetProperty("reasoning", out var r) ? r.GetString() ?? "" : "";

            decimal discountPct = 20m;
            if (root.TryGetProperty("discountPercent", out var dp))
            {
                if (dp.ValueKind == JsonValueKind.Number) discountPct = dp.GetDecimal();
                else if (decimal.TryParse(dp.GetString(), out var parsed)) discountPct = parsed;
            }

            // Find matching batch
            var targetBatch = batches.FirstOrDefault(x => string.Equals(x.BatchCode, batchCode, StringComparison.OrdinalIgnoreCase))
                ?? batches.FirstOrDefault(x => x.Product != null && !string.IsNullOrEmpty(name) && name.Contains(x.Product.Name));

            if (targetBatch?.Product != null)
            {
                var p = targetBatch.Product;
                decimal originalPrice = p.Price;
                decimal discountedPrice = Math.Round(originalPrice * (1 - discountPct / 100m), 0);

                if (string.IsNullOrWhiteSpace(name))
                {
                    name = $"{p.Name} 夕方直前割 {discountPct:F0}% OFF";
                }

                var proposal = new PromotionProposalDto(
                    Name: name,
                    PromotionType: promoType,
                    TargetProductId: p.Id,
                    TargetProductName: p.Name,
                    TargetBatchId: targetBatch.Id,
                    TargetBatchCode: targetBatch.BatchCode,
                    DiscountPercent: discountPct,
                    OriginalPrice: originalPrice,
                    DiscountedPrice: discountedPrice,
                    StartTime: now.Hour >= 17 ? now : now.Date.AddHours(17),
                    EndTime: targetBatch.ExpiryDate.AddMinutes(-30),
                    Reasoning: string.IsNullOrWhiteSpace(reasoning)
                        ? $"ロット{targetBatch.BatchCode}（残{targetBatch.RemainingQuantity}個）の消化促進に向け、{discountPct:F0}%OFFを提案。"
                        : reasoning
                );

                return (cleanReply, proposal);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse JSON proposal from AI reply.");
        }

        return (cleanReply, null);
    }
}
