using System.Text.Json;
using System.Text.RegularExpressions;
using RetailRescueAI.Backend.DTOs;
using RetailRescueAI.Backend.Models;
using RetailRescueAI.Backend.Repositories.Interfaces;
using RetailRescueAI.Backend.Services.AI;
using RetailRescueAI.Backend.Services.Interfaces;

namespace RetailRescueAI.Backend.Services.Implementations;

public class ChatbotService : IChatbotService
{
    private readonly IInventoryBatchRepository _batchRepository;
    private readonly ILLMService _llmService;
    private readonly ILogger<ChatbotService> _logger;

    public ChatbotService(
        IInventoryBatchRepository batchRepository,
        ILLMService llmService,
        ILogger<ChatbotService> logger)
    {
        _batchRepository = batchRepository;
        _llmService = llmService;
        _logger = logger;
    }

    public async Task<ChatResponse> ProcessChatAsync(ChatRequest request, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        // Fetch all active batches (remaining > 0, expiry > now)
        var allBatches = await _batchRepository.GetActiveBatchesAsync(cancellationToken);
        var activeBatches = allBatches.Where(b => b.ExpiryDate > now && b.RemainingQuantity > 0).ToList();

        var contextSummary = string.Join("\n", activeBatches.Select(b =>
            $"- {b.Product?.Name} (ロット: {b.BatchCode}, 商品ID: {b.ProductId}): 残り{b.RemainingQuantity}個, 定価¥{b.Product?.Price}, 原価¥{b.Product?.CostPrice}, 賞味期限まで残り{(b.ExpiryDate - now).TotalHours:F1}時間, リスク状態: {b.Status}"));

        var systemPrompt = $@"あなたはスーパーマーケット「ライフマート新宿店」の店長専属AIアシスタント（RetailRescue AI）です。
店長からの在庫状況、賞味期限切れ間近商品の対応、割引・プロモーションに関する相談に、礼儀正しく論理的な日本語で回答してください。

【現在の店舗内ロット在庫データ】:
{contextSummary}

【業務ルール】:
1. 割引プロモーションの提案は、夕方ピーク（17:00〜22:00）や賞味期限前（BR-003）を推奨すること。
2. 割引率は20%〜30%を基本とし、最低粗利率（15%）を維持できることを論理的に説明すること。
3. 店長が特定の割引率を指定した場合（例：「たまごサンドを30%引きにして」「25%で登録」）、その指定を尊重して提案を構成すること。
4. 店長が作成を依頼した場合、必ず「承認待ち（PENDING）として登録します」と案内すること（勝手に有効化しない）。
5. 【プロモーション提案データ出力フォーマット】:
店長に対して具体的な値引きプロモーションを提案・合意する場合、回答本文の末尾に必ず以下のJSONブロックを正確に付与してください（一般的な質問には不要）:

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
        var reply = await _llmService.ChatAsync(systemPrompt, history, request.Message, cancellationToken);

        // Step 1: Try to extract structured JSON proposal from AI reply
        var (cleanReply, proposal) = TryExtractJsonProposal(reply, activeBatches, now);

        // Step 2: If no JSON block found, perform smart semantic extraction based on userMessage & reply
        if (proposal == null)
        {
            proposal = TrySemanticProposalExtraction(request.Message, cleanReply, activeBatches, now);
        }

        return new ChatResponse(cleanReply, proposal != null, proposal);
    }

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

    private PromotionProposalDto? TrySemanticProposalExtraction(
        string userMessage,
        string reply,
        List<InventoryBatch> batches,
        DateTime now)
    {
        var combinedText = $"{userMessage} {reply}".ToLower();

        // Check if intent is related to promotions, discounts, or crisis action
        bool hasPromoIntent = combinedText.Contains("割引") || combinedText.Contains("プロモーション") ||
                              combinedText.Contains("提案") || combinedText.Contains("オフ") ||
                              combinedText.Contains("off") || combinedText.Contains("引き") ||
                              combinedText.Contains("安く") || combinedText.Contains("登録") ||
                              combinedText.Contains("作成") || combinedText.Contains("値下げ") ||
                              combinedText.Contains("セール") || combinedText.Contains("％") ||
                              combinedText.Contains("%");

        if (!hasPromoIntent) return null;

        // 1. Identify target batch dynamically
        InventoryBatch? targetBatch = null;

        // A. Match by exact batch code mentioned in message or reply
        foreach (var b in batches)
        {
            if (combinedText.Contains(b.BatchCode.ToLower()))
            {
                targetBatch = b;
                break;
            }
        }

        // B. Match by product keyword
        if (targetBatch == null)
        {
            if (combinedText.Contains("サンド") || combinedText.Contains("たまご") || combinedText.Contains("egg") || combinedText.Contains("sandwich"))
            {
                targetBatch = batches.OrderBy(b => b.ExpiryDate).FirstOrDefault(b => b.Product != null && (b.Product.Name.Contains("サンド") || b.Product.ProductCode.Contains("SAND")));
            }
            else if (combinedText.Contains("かつ丼") || combinedText.Contains("ロース") || combinedText.Contains("pork") || combinedText.Contains("cutlet"))
            {
                targetBatch = batches.OrderBy(b => b.ExpiryDate).FirstOrDefault(b => b.Product != null && (b.Product.Name.Contains("かつ丼") || b.Product.ProductCode.Contains("BENTO-002") || b.Product.ProductCode.Contains("BENTO-003")));
            }
            else if (combinedText.Contains("サラダ") || combinedText.Contains("サーモン") || combinedText.Contains("salmon") || combinedText.Contains("salad"))
            {
                targetBatch = batches.OrderBy(b => b.ExpiryDate).FirstOrDefault(b => b.Product != null && (b.Product.Name.Contains("サラダ") || b.Product.ProductCode.Contains("SALAD")));
            }
            else if (combinedText.Contains("チキン") || combinedText.Contains("南蛮") || combinedText.Contains("chicken") || combinedText.Contains("弁当") || combinedText.Contains("bento"))
            {
                targetBatch = batches.OrderBy(b => b.ExpiryDate).FirstOrDefault(b => b.Product != null && (b.Product.Name.Contains("チキン") || b.Product.ProductCode.Contains("BENTO-001")));
            }
        }

        // C. If general question about urgent items, pick highest risk / earliest expiry
        if (targetBatch == null)
        {
            targetBatch = batches.OrderBy(b => b.ExpiryDate).FirstOrDefault(b => b.Status == "CRITICAL")
                       ?? batches.OrderBy(b => b.ExpiryDate).FirstOrDefault(b => b.Status == "AT_RISK")
                       ?? batches.OrderBy(b => b.ExpiryDate).FirstOrDefault();
        }

        if (targetBatch?.Product == null) return null;

        var product = targetBatch.Product;

        // 2. Extract discount percent dynamically
        decimal discount = 20m;

        // Search userMessage first
        var matchUser = Regex.Match(userMessage, @"(\d{1,2})\s*(?:%|％)");
        if (matchUser.Success && decimal.TryParse(matchUser.Groups[1].Value, out var uPct))
        {
            discount = Math.Clamp(uPct, 5, 70);
        }
        else if (userMessage.Contains("3割") || userMessage.Contains("30%"))
        {
            discount = 30m;
        }
        else if (userMessage.Contains("半額") || userMessage.Contains("50%"))
        {
            discount = 50m;
        }
        else
        {
            // Search reply
            var matchReply = Regex.Match(reply, @"(\d{1,2})\s*(?:%|％)");
            if (matchReply.Success && decimal.TryParse(matchReply.Groups[1].Value, out var rPct))
            {
                discount = Math.Clamp(rPct, 5, 70);
            }
            else if (targetBatch.Status == "CRITICAL")
            {
                discount = 30m;
            }
            else if (targetBatch.Status == "AT_RISK")
            {
                discount = 20m;
            }
            else
            {
                discount = 15m;
            }
        }

        decimal originalPrice = product.Price;
        decimal discountedPrice = Math.Round(originalPrice * (1 - discount / 100m), 0);

        return new PromotionProposalDto(
            Name: $"{product.Name} 夕方直前割 {discount:F0}% OFF",
            PromotionType: "DIRECT_DISCOUNT",
            TargetProductId: product.Id,
            TargetProductName: product.Name,
            TargetBatchId: targetBatch.Id,
            TargetBatchCode: targetBatch.BatchCode,
            DiscountPercent: discount,
            OriginalPrice: originalPrice,
            DiscountedPrice: discountedPrice,
            StartTime: now.Hour >= 17 ? now : now.Date.AddHours(17),
            EndTime: targetBatch.ExpiryDate.AddMinutes(-30),
            Reasoning: $"残り{targetBatch.RemainingQuantity}個の早期完売に向け、夕方ピーク帯に{discount:F0}%OFF（¥{discountedPrice:N0}）のプロモーションを推奨します。"
        );
    }
}

