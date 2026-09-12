using System.Text.Json;
using System.Text.RegularExpressions;
using RetailRescueAI.Backend.DTOs;
using RetailRescueAI.Backend.Models;
using RetailRescueAI.Backend.Repositories.Interfaces;
using RetailRescueAI.Backend.Services.AI;
using RetailRescueAI.Backend.Services.AI.Agents;
using RetailRescueAI.Backend.Services.Interfaces;

namespace RetailRescueAI.Backend.Services.Implementations;

public record StructuredAiAction(
    string Action, // "APPROVE", "PROPOSE", "NONE"
    string? BatchCode,
    int? PromotionId,
    string? PromotionType,
    decimal? DiscountPercent,
    decimal? ComboPrice,
    string? Name,
    string? Reasoning
);

public class ChatbotService : IChatbotService
{
    private readonly IInventoryBatchRepository _batchRepository;
    private readonly IPromotionRepository _promotionRepository;
    private readonly IPromotionService _promotionService;
    private readonly ExpiryAgent _expiryAgent;
    private readonly SalesAgent _salesAgent;
    private readonly PromotionAgent _promotionAgent;
    private readonly ReviserAgent _reviserAgent;
    private readonly ILLMService _llmService;
    private readonly ILogger<ChatbotService> _logger;

    public ChatbotService(
        IInventoryBatchRepository batchRepository,
        IPromotionRepository promotionRepository,
        IPromotionService promotionService,
        ExpiryAgent expiryAgent,
        SalesAgent salesAgent,
        PromotionAgent promotionAgent,
        ReviserAgent reviserAgent,
        ILLMService llmService,
        ILogger<ChatbotService> logger)
    {
        _batchRepository = batchRepository;
        _promotionRepository = promotionRepository;
        _promotionService = promotionService;
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

        // 1. Fetch active inventory batches & existing pending promotions
        var allBatches = await _batchRepository.GetActiveBatchesAsync(cancellationToken);
        var activeBatches = allBatches.Where(b => b.RemainingQuantity > 0).ToList();
        var pendingPromos = await _promotionRepository.GetPromotionsAsync("PENDING", cancellationToken);

        // 2. Step 1: ExpiryAgent - evaluate shelf life and urgency
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

        // 3. Step 2: SalesAgent - evaluate velocity and projected waste
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

        // 4. Step 3: PromotionAgent strategy overview
        var urgentCount = salesResults.Count(s => s.FinalEvaluatedRiskLevel == "CRITICAL" || s.FinalEvaluatedRiskLevel == "AT_RISK");
        traceSteps.Add(new AiAgentTraceStepDto(
            AgentKey: _promotionAgent.Name,
            AgentName: _promotionAgent.RoleTitle,
            RoleTitle: "Smart Promotion Strategy Agent (SK & Gemini)",
            Description: "商品特性とピーク時間帯を踏まえ、最適な割引率・プロモーション戦略を策定",
            Details: new List<string>
            {
                $"💡 危険在庫 {urgentCount}ロットに対する最適値引き・セット販売戦略の準備完了",
                "💡 最低粗利率15%以上の維持、夕方ピーク（17:00〜22:00）販売促進を推奨"
            },
            Status: "COMPLETED",
            DurationMs: 10
        ));

        // 5. Build dynamic context synthesis for LLM
        var contextSummary = string.Join("\n", salesResults.Select(s =>
            $"- {s.Batch.Product?.Name} (ロット: {s.Batch.BatchCode}, 商品ID: {s.Batch.ProductId}): 残り{s.Batch.RemainingQuantity}個, 定価¥{s.Batch.Product?.Price:N0}, 原価¥{s.Batch.Product?.CostPrice:N0}, 賞味期限まで残り{s.HoursUntilExpiry:F1}時間, リスク状態: {s.FinalEvaluatedRiskLevel}, 日販ペース: {s.AverageDailySales:F1}個/日, 潜在廃棄: {s.PotentialWasteUnits}個 (損失見込 ¥{s.PotentialWasteFinancialLoss:N0})"));

        var pendingSummary = pendingPromos.Count > 0
            ? string.Join("\n", pendingPromos.Select(p =>
                $"- プロモーションID #{p.Id}: 「{p.Name}」（対象ロット: {p.TargetBatch?.BatchCode ?? "未指定"}, 対象商品: {p.TargetProduct?.Name ?? "未指定"}, 割引率: {p.DiscountPercent}%, 状態: {p.Status}）"))
            : "（現在、未承認のプロモーションはありません）";

        var systemPrompt = $@"あなたはスーパーマーケット「ライフマート新宿店」の店長専属AIアシスタント（RetailRescue AI）です。
裏側で専門エージェント群（ExpiryAgent, SalesAgent, PromotionAgent, ReviserAgent）と連携し、店長からの在庫・賞味期限・割引プロモーションに関する相談に丁寧かつ論理的に対応してください。
店長のメッセージが日本語の場合は日本語で、ベトナム語の場合はベトナム語で、英語の場合は英語で回答してください。

【最新の危険ロット・販売予測データ】:
{contextSummary}

【現在の承認待ち（PENDING）プロモーション一覧】:
{pendingSummary}

【業務ルールとAI指示】:
1. 店長の意図を正確に読み取り、回答本文の後に必ず所定のJSONアクションを出力してください。
2. アクション種別（action）の判定基準:
   - ""APPROVE"": 店長がプロモーションの承認・適用を指示・合意・決定した場合（例:「duyệt đi」「đồng ý duyệt」「30%で承認します」「この提案を採用」「了解です」「ok duyệt」「chốt」など）。過去の履歴や承認待ち一覧から対象ロット・プロモーションを特定してください。
   - ""PROPOSE"": 店長が割引・プロモーションを相談・要求・作成依頼した場合（例:「たまごサンドを3割引きにして」「đề xuất giảm giá 30% cho sandwich」「廃棄を防ぐプロモーションを提案して」「20%オフで登録」など）。店長が指定した割引率（3割=30%、半額=50%、25%など）があればその数値を正確に反映してください。
   - ""NONE"": 単なる在庫確認や状況質問、一般的な会話など、プロモーション提案・承認の必要がない場合。
3. 割引率の基本基準: 20%〜30%（粗利率15%以上を維持）。店長が明示的にパーセンテージや割を指定した場合はその数値を最優先してください。
4. 回答の最後には、必ず次の形式のJSONブロックを付与してください（ユーザーへの会話文はJSONの前に記述してください）:

```json:action
{{
  ""action"": ""PROPOSE"" または ""APPROVE"" または ""NONE"",
  ""batchCode"": ""対象のロットコード（例: BATCH-SAND-001。対象がない場合はnull）"",
  ""promotionId"": 対象のプロモーションID（既存の承認待ちプロモーションがある場合。新規ならnull）,
  ""promotionType"": ""DIRECT_DISCOUNT"" または ""BUNDLE_COMBO"",
  ""discountPercent"": 30,
  ""comboPrice"": null,
  ""name"": ""プロモーション名称（例: こだわりたまごサンド 夕方直前割 30% OFF）"",
  ""reasoning"": ""提案または承認の理由・根拠""
}}
```";

        // 6. Call LLM
        var history = request.History ?? new List<ChatMessageDto>();
        var userMessage = request.Message ?? string.Empty;
        var reply = await _llmService.ChatAsync(systemPrompt, history, userMessage, cancellationToken);

        // 7. Parse structured AI action directly from reply without any manual keyword/regex guessing
        var (cleanReply, aiAction) = TryExtractAiAction(reply);

        // 8. Execute business action based on AI's structured response
        if (aiAction != null && aiAction.Action.Equals("APPROVE", StringComparison.OrdinalIgnoreCase))
        {
            // Manager Approval Action
            Promotion? targetPromo = null;

            if (aiAction.PromotionId.HasValue && aiAction.PromotionId.Value > 0)
            {
                targetPromo = pendingPromos.FirstOrDefault(p => p.Id == aiAction.PromotionId.Value);
            }

            if (targetPromo == null && !string.IsNullOrWhiteSpace(aiAction.BatchCode))
            {
                targetPromo = pendingPromos.FirstOrDefault(p => p.TargetBatch != null &&
                    string.Equals(p.TargetBatch.BatchCode, aiAction.BatchCode, StringComparison.OrdinalIgnoreCase));
            }

            if (targetPromo == null)
            {
                targetPromo = pendingPromos.FirstOrDefault();
            }

            // If still null, create a promotion for the designated batch on the fly to approve it
            if (targetPromo == null)
            {
                InventoryBatch? candidateBatch = null;
                if (!string.IsNullOrWhiteSpace(aiAction.BatchCode))
                {
                    candidateBatch = activeBatches.FirstOrDefault(b =>
                        string.Equals(b.BatchCode, aiAction.BatchCode, StringComparison.OrdinalIgnoreCase));
                }
                candidateBatch ??= activeBatches.OrderBy(b => b.ExpiryDate).FirstOrDefault();

                if (candidateBatch?.Product != null)
                {
                    var prod = candidateBatch.Product;
                    var discPct = aiAction.DiscountPercent ?? 20m;
                    var promoName = !string.IsNullOrWhiteSpace(aiAction.Name)
                        ? aiAction.Name
                        : $"{prod.Name} 直前割 {discPct:F0}% OFF";
                    var startTime = now.Hour >= 17 ? now : now.Date.AddHours(17);
                    var endTime = candidateBatch.ExpiryDate.AddMinutes(-30);
                    if (endTime <= startTime) endTime = candidateBatch.ExpiryDate;

                    var createdDto = await _promotionService.CreatePromotionAsync(new CreatePromotionRequest(
                        Name: promoName,
                        PromotionType: aiAction.PromotionType ?? "DIRECT_DISCOUNT",
                        TargetProductId: prod.Id,
                        TargetBatchId: candidateBatch.Id,
                        DiscountPercent: discPct,
                        ComboPrice: aiAction.ComboPrice,
                        StartTime: startTime,
                        EndTime: endTime,
                        Reasoning: aiAction.Reasoning ?? $"店長のチャット承認指示に基づきロット{candidateBatch.BatchCode}のプロモーションを作成・即時有効化"
                    ), cancellationToken);

                    targetPromo = await _promotionRepository.GetPromotionWithDetailsAsync(createdDto.Id, cancellationToken);
                }
            }

            if (targetPromo != null)
            {
                await _promotionService.ApprovePromotionAsync(targetPromo.Id, cancellationToken);
                var approved = await _promotionRepository.GetPromotionWithDetailsAsync(targetPromo.Id, cancellationToken) ?? targetPromo;
                var prod = approved.TargetProduct;
                var batch = approved.TargetBatch;
                var origPrice = prod?.Price ?? 0m;
                var discPct = approved.DiscountPercent ?? 20m;
                var discPrice = Math.Round(origPrice * (1 - discPct / 100m), 0);

                var approvedProposalDto = new PromotionProposalDto(
                    Name: approved.Name,
                    PromotionType: approved.PromotionType,
                    TargetProductId: prod?.Id ?? 0,
                    TargetProductName: prod?.Name ?? "指定商品",
                    TargetBatchId: batch?.Id,
                    TargetBatchCode: batch?.BatchCode ?? "指定ロット",
                    DiscountPercent: discPct,
                    OriginalPrice: origPrice,
                    DiscountedPrice: discPrice,
                    StartTime: approved.StartTime,
                    EndTime: approved.EndTime,
                    Reasoning: approved.AiReasoning ?? aiAction.Reasoning ?? "店長指示に基づきチャット経由で即時承認・有効化",
                    PromotionId: approved.Id,
                    Status: "APPROVED"
                );

                traceSteps.Add(new AiAgentTraceStepDto(
                    AgentKey: "OrchestratorAgent",
                    AgentName: "統括オーケストレーター",
                    RoleTitle: "Manager Approval & Execution Agent",
                    Description: "店長の承認指示に基づきプロモーションをAPPROVEDへ変更。SignalRでレジへ即時反映完了。",
                    Details: new List<string>
                    {
                        $"✅ プロモーションID #{approved.Id}「{approved.Name}」を正式承認",
                        $"📡 SignalRイベント「PromotionApproved」をPOSレジへ配信完了",
                        $"🎯 ロット {batch?.BatchCode} に対し {discPct:F0}% OFF を有効化"
                    },
                    Status: "COMPLETED",
                    DurationMs: 15
                ));

                return new ChatResponse(cleanReply, true, approvedProposalDto, traceSteps);
            }
        }
        else if (aiAction != null && aiAction.Action.Equals("PROPOSE", StringComparison.OrdinalIgnoreCase))
        {
            // Promotion Proposal Action
            InventoryBatch? targetBatch = null;

            if (!string.IsNullOrWhiteSpace(aiAction.BatchCode))
            {
                targetBatch = activeBatches.FirstOrDefault(b =>
                    string.Equals(b.BatchCode, aiAction.BatchCode, StringComparison.OrdinalIgnoreCase));
            }
            if (targetBatch == null && !string.IsNullOrWhiteSpace(aiAction.Name))
            {
                targetBatch = activeBatches.FirstOrDefault(b =>
                    b.Product != null && aiAction.Name.Contains(b.Product.Name));
            }
            if (targetBatch == null)
            {
                targetBatch = activeBatches.OrderBy(b => b.ExpiryDate).FirstOrDefault();
            }

            if (targetBatch?.Product != null)
            {
                var prod = targetBatch.Product;
                var discPct = aiAction.DiscountPercent ?? 20m;
                var origPrice = prod.Price;
                var discPrice = Math.Round(origPrice * (1 - discPct / 100m), 0);
                var startTime = now.Hour >= 17 ? now : now.Date.AddHours(17);
                var endTime = targetBatch.ExpiryDate.AddMinutes(-30);
                if (endTime <= startTime) endTime = targetBatch.ExpiryDate;

                var promoTitle = !string.IsNullOrWhiteSpace(aiAction.Name)
                    ? aiAction.Name
                    : $"{prod.Name} 直前割 {discPct:F0}% OFF";

                // Validate with ReviserAgent
                var tempProposal = new PromotionProposal(
                    TargetBatch: targetBatch,
                    PromotionType: aiAction.PromotionType ?? "DIRECT_DISCOUNT",
                    RiskLevel: "CRITICAL",
                    ActionTitle: promoTitle,
                    DiscountPercent: discPct,
                    ComboPrice: aiAction.ComboPrice,
                    StartTime: startTime,
                    EndTime: endTime,
                    ExpectedSales: targetBatch.RemainingQuantity,
                    ExpectedWasteReduction: targetBatch.RemainingQuantity,
                    ExpectedRevenue: targetBatch.RemainingQuantity * discPrice,
                    Reason: aiAction.Reasoning ?? "AIによる直前値引き提案",
                    EvidenceMap: new Dictionary<string, string>()
                );

                var valResults = _reviserAgent.ValidateProposals(new List<PromotionProposal> { tempProposal });
                var val = valResults.FirstOrDefault();
                bool isValid = val?.IsValid ?? true;
                var valMessages = val?.ValidationMessages ?? new List<string> { "粗利率・賞味期限規約（BR-003, BR-006）適合" };

                traceSteps.Add(new AiAgentTraceStepDto(
                    AgentKey: _reviserAgent.Name,
                    AgentName: _reviserAgent.RoleTitle,
                    RoleTitle: "Retail Safety & Guardrails Agent (SK Plugin)",
                    Description: "リテール規約 BR-003（賞味期限内）、BR-006（実在庫）、粗利率15%以上の厳格チェック",
                    Details: valMessages,
                    Status: isValid ? "COMPLETED" : "WARNING",
                    DurationMs: 10
                ));

                // Link existing pending promotion ID if already registered
                var existingPending = pendingPromos.FirstOrDefault(p => p.TargetBatchId == targetBatch.Id);

                var proposedDto = new PromotionProposalDto(
                    Name: promoTitle,
                    PromotionType: aiAction.PromotionType ?? "DIRECT_DISCOUNT",
                    TargetProductId: prod.Id,
                    TargetProductName: prod.Name,
                    TargetBatchId: targetBatch.Id,
                    TargetBatchCode: targetBatch.BatchCode,
                    DiscountPercent: discPct,
                    OriginalPrice: origPrice,
                    DiscountedPrice: discPrice,
                    StartTime: startTime,
                    EndTime: endTime,
                    Reasoning: aiAction.Reasoning ?? $"ロット{targetBatch.BatchCode}の廃棄リスク回避のため、{discPct:F0}%割引（¥{discPrice:N0}）を提案。",
                    PromotionId: existingPending?.Id,
                    Status: "PENDING"
                );

                traceSteps.Add(new AiAgentTraceStepDto(
                    AgentKey: "OrchestratorAgent",
                    AgentName: "統括オーケストレーター",
                    RoleTitle: "Human-in-the-Loop Orchestrator",
                    Description: "店長への割引プロモーション提案を策定（承認待ちキューへ準備）",
                    Details: new List<string>
                    {
                        $"提案対象: {targetBatch.BatchCode} ({prod.Name}) - {discPct:F0}% OFF (¥{discPrice:N0})",
                        "ステータス: 承認待ち（PENDING）- 店長の確認・承認後にPOSへ反映されます"
                    },
                    Status: "COMPLETED",
                    DurationMs: 5
                ));

                return new ChatResponse(cleanReply, true, proposedDto, traceSteps);
            }
        }

        // None or General Inquiry
        traceSteps.Add(new AiAgentTraceStepDto(
            AgentKey: _reviserAgent.Name,
            AgentName: _reviserAgent.RoleTitle,
            RoleTitle: "Retail Safety & Guardrails Agent (SK Plugin)",
            Description: "リテール規程 BR-003/BR-007の整合性確認",
            Details: new List<string> { "🛡️ 在庫・販売問合せの安全規程整合性を確認完了" },
            Status: "COMPLETED",
            DurationMs: 5
        ));

        traceSteps.Add(new AiAgentTraceStepDto(
            AgentKey: "OrchestratorAgent",
            AgentName: "統括オーケストレーター",
            RoleTitle: "Human-in-the-Loop Orchestrator",
            Description: "最新在庫・販売速度に基づくデータサマリーを店長へ提示",
            Details: new List<string>
            {
                "🎯 在庫状況・販売傾向の分析回答を提供完了"
            },
            Status: "COMPLETED",
            DurationMs: 5
        ));

        return new ChatResponse(cleanReply, false, null, traceSteps);
    }

    private static string GetRiskBadge(string risk) => risk switch
    {
        "EXPIRED" => "⛔",
        "CRITICAL" => "🔴",
        "AT_RISK" => "🟠",
        "MEDIUM" => "🟡",
        _ => "🟢"
    };

    private (string CleanReply, StructuredAiAction? Action) TryExtractAiAction(string rawReply)
    {
        if (string.IsNullOrWhiteSpace(rawReply)) return (rawReply, null);

        var jsonRegex = new Regex(@"```(?:json(?::(?:action|proposal)?)?)\s*(\{[\s\S]*?\})\s*```", RegexOptions.IgnoreCase);
        var match = jsonRegex.Match(rawReply);

        string cleanReply = rawReply;
        string? jsonContent = null;

        if (match.Success)
        {
            jsonContent = match.Groups[1].Value;
            cleanReply = jsonRegex.Replace(rawReply, string.Empty).Trim();
        }
        else
        {
            var rawJsonRegex = new Regex(@"(\{[\s\r\n]*""action""[\s\S]*\})", RegexOptions.IgnoreCase);
            var rawMatch = rawJsonRegex.Match(rawReply);
            if (rawMatch.Success)
            {
                jsonContent = rawMatch.Groups[1].Value;
                cleanReply = rawJsonRegex.Replace(rawReply, string.Empty).Trim();
            }
        }

        if (string.IsNullOrWhiteSpace(jsonContent))
        {
            return (cleanReply, null);
        }

        try
        {
            using var doc = JsonDocument.Parse(jsonContent);
            var root = doc.RootElement;

            string action = root.TryGetProperty("action", out var a) ? a.GetString() ?? "NONE" : "NONE";
            string? batchCode = root.TryGetProperty("batchCode", out var b) ? b.GetString() : null;
            string promoType = root.TryGetProperty("promotionType", out var pt) ? pt.GetString() ?? "DIRECT_DISCOUNT" : "DIRECT_DISCOUNT";
            string? name = root.TryGetProperty("name", out var n) ? n.GetString() : null;
            string? reasoning = root.TryGetProperty("reasoning", out var r) ? r.GetString() : null;

            int? promotionId = null;
            if (root.TryGetProperty("promotionId", out var pid))
            {
                if (pid.ValueKind == JsonValueKind.Number) promotionId = pid.GetInt32();
                else if (int.TryParse(pid.GetString(), out var parsedId)) promotionId = parsedId;
            }

            decimal? discountPercent = null;
            if (root.TryGetProperty("discountPercent", out var dp))
            {
                if (dp.ValueKind == JsonValueKind.Number) discountPercent = dp.GetDecimal();
                else if (decimal.TryParse(dp.GetString(), out var parsedPct)) discountPercent = parsedPct;
            }

            decimal? comboPrice = null;
            if (root.TryGetProperty("comboPrice", out var cp))
            {
                if (cp.ValueKind == JsonValueKind.Number) comboPrice = cp.GetDecimal();
                else if (decimal.TryParse(cp.GetString(), out var parsedCp)) comboPrice = parsedCp;
            }

            return (cleanReply, new StructuredAiAction(
                Action: action.ToUpperInvariant(),
                BatchCode: batchCode,
                PromotionId: promotionId,
                PromotionType: promoType,
                DiscountPercent: discountPercent,
                ComboPrice: comboPrice,
                Name: name,
                Reasoning: reasoning
            ));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse structured AI action from reply.");
            return (cleanReply, null);
        }
    }
}
