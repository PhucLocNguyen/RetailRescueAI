using RetailRescueAI.Backend.Models;

namespace RetailRescueAI.Backend.Services.AI.Agents;

public record ValidatedProposalResult(
    PromotionProposal Proposal,
    bool IsValid,
    List<string> ValidationMessages
);

public class ReviserAgent
{
    private readonly ILogger<ReviserAgent> _logger;

    public ReviserAgent(ILogger<ReviserAgent> logger)
    {
        _logger = logger;
    }

    public List<ValidatedProposalResult> ValidateProposals(List<PromotionProposal> proposals)
    {
        _logger.LogInformation("[ReviserAgent] Validating {Count} proposals against retail business rules & safety guardrails...", proposals.Count);

        var validated = new List<ValidatedProposalResult>();

        foreach (var proposal in proposals)
        {
            var messages = new List<string>();
            bool isValid = true;
            var batch = proposal.TargetBatch;
            var product = batch.Product!;

            // 1. BR-003: Promotion End Time must NOT exceed Batch Expiry Time
            if (proposal.EndTime > batch.ExpiryDate)
            {
                isValid = false;
                messages.Add($"[BR-003 違反] プロモーション終了日時 ({proposal.EndTime:yyyy-MM-dd HH:mm}) が賞味期限 ({batch.ExpiryDate:yyyy-MM-dd HH:mm}) を超えています。");
            }
            else
            {
                messages.Add($"[BR-003 適合] プロモーション期間は賞味期限内に収まっています。");
            }

            // 2. BR-006: Inventory Validation (Must have available stock)
            if (batch.RemainingQuantity <= 0)
            {
                isValid = false;
                messages.Add($"[BR-006 違反] 対象ロット ({batch.BatchCode}) の残在庫数が0以下です。");
            }
            else
            {
                messages.Add($"[BR-006 適合] 対象ロットの有効在庫あり（{batch.RemainingQuantity}個）。");
            }

            // 3. Max Discount Constraint
            if (proposal.DiscountPercent.HasValue && proposal.DiscountPercent.Value > product.MaxDiscountPercent)
            {
                isValid = false;
                messages.Add($"[割引上限 違反] 提案割引率 ({proposal.DiscountPercent}%) が商品上限 ({product.MaxDiscountPercent}%) を超過しています。");
            }

            // 4. Minimum Margin Constraint
            if (proposal.DiscountPercent.HasValue)
            {
                decimal sellingPriceAfterDiscount = product.Price * (1.0m - proposal.DiscountPercent.Value / 100.0m);
                decimal minRequiredPrice = product.CostPrice * (1.0m + product.MinMarginPercent / 100.0m);

                if (sellingPriceAfterDiscount < minRequiredPrice)
                {
                    // If selling below min margin, log warning or adjust
                    messages.Add($"[粗利警告] 割引後価格 (¥{sellingPriceAfterDiscount:N0}) は最低目標粗利価格 (¥{minRequiredPrice:N0}) を下回りますが、廃棄損失（¥{product.CostPrice:N0}/個）全損回避の観点から許容範囲と判定。");
                }
                else
                {
                    messages.Add($"[粗利基準 適合] 割引後粗利率を確保（最低利益率 {product.MinMarginPercent}% クリア）。");
                }
            }

            validated.Add(new ValidatedProposalResult(proposal, isValid, messages));
        }

        return validated;
    }
}

