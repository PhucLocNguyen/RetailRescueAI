using Microsoft.EntityFrameworkCore;
using RetailRescueAI.Backend.Data;
using RetailRescueAI.Backend.DTOs;

namespace RetailRescueAI.Backend.Services.AI;

public class ManagerChatbotService
{
    private readonly AppDbContext _context;
    private readonly ILLMService _llmService;
    private readonly ILogger<ManagerChatbotService> _logger;

    public ManagerChatbotService(
        AppDbContext context,
        ILLMService llmService,
        ILogger<ManagerChatbotService> logger)
    {
        _context = context;
        _llmService = llmService;
        _logger = logger;
    }

    public async Task<ChatResponse> ProcessChatAsync(ChatRequest request, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        // Fetch at-risk batches context
        var urgentBatches = await _context.InventoryBatches
            .Include(b => b.Product)
            .Where(b => b.RemainingQuantity > 0 && (b.Status == "CRITICAL" || b.Status == "AT_RISK"))
            .ToListAsync(cancellationToken);

        var contextSummary = string.Join("\n", urgentBatches.Select(b =>
            $"- {b.Product?.Name} (ロット: {b.BatchCode}): 残り{b.RemainingQuantity}個, 定価¥{b.Product?.Price}, 賞味期限まで残り{(b.ExpiryDate - now).TotalHours:F1}時間, 状態: {b.Status}"));

        var systemPrompt = $@"あなたはスーパーマーケット「ライフマート新宿店」の店長専属AIアシスタント（RetailRescue AI）です。
店長からの在庫、賞味期限切れ、割引プロモーションに関する相談に、礼儀正しく論理的な日本語で回答してください。

【現在の危険在庫データ】:
{contextSummary}

【ルール】:
1. 割引プロモーションの提案は、夕方ピーク（17:00〜22:00）や賞味期限前（BR-003）を推奨すること。
2. 割引率は20%〜30%を基本とし、最低粗利率（15%）を維持できることを伝えること。
3. 店長が作成を依頼した場合、必ず「承認待ち（PENDING）として登録します」と案内すること（勝手に有効化しない）。";

        var history = request.History ?? new List<ChatMessageDto>();
        var reply = await _llmService.ChatAsync(systemPrompt, history, request.Message, cancellationToken);

        // Check if message asks to propose or discount chicken bento
        PromotionProposalDto? proposal = null;
        var msgLower = request.Message.ToLower();
        var targetBatch = urgentBatches.FirstOrDefault(b => b.Product != null && (b.Product.Name.Contains("チキン") || b.Product.ProductCode.Contains("BENTO-001")));

        if (targetBatch != null && (msgLower.Contains("チキン") || msgLower.Contains("割引") || msgLower.Contains("プロモーション") || msgLower.Contains("提案")))
        {
            var p = targetBatch.Product!;
            decimal discount = 20m;
            decimal discountedPrice = p.Price * (1 - discount / 100m);

            proposal = new PromotionProposalDto(
                Name: $"{p.Name} 夕方直前割 {discount:F0}% OFF",
                PromotionType: "DIRECT_DISCOUNT",
                TargetProductId: p.Id,
                TargetProductName: p.Name,
                TargetBatchId: targetBatch.Id,
                TargetBatchCode: targetBatch.BatchCode,
                DiscountPercent: discount,
                OriginalPrice: p.Price,
                DiscountedPrice: discountedPrice,
                StartTime: now.Date.AddHours(17),
                EndTime: targetBatch.ExpiryDate.AddMinutes(-30),
                Reasoning: $"残り{targetBatch.RemainingQuantity}個の早期完売に向け、17:00〜22:00の夕方ピーク帯に20%OFF（¥{discountedPrice:N0}）のプロモーションを推奨します。"
            );
        }

        return new ChatResponse(reply, proposal != null, proposal);
    }
}

