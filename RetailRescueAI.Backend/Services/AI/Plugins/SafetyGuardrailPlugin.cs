using System.ComponentModel;
using Microsoft.SemanticKernel;

namespace RetailRescueAI.Backend.Services.AI.Plugins;

public class SafetyGuardrailPlugin
{
    private readonly ILogger<SafetyGuardrailPlugin> _logger;

    public SafetyGuardrailPlugin(ILogger<SafetyGuardrailPlugin> logger)
    {
        _logger = logger;
    }

    [KernelFunction, Description("Validates proposed discount against retail business rules (BR-003 expiry, BR-006 stock, and 15% minimum margin)")]
    public string ValidatePromotionSafety(
        [Description("Proposed discount percent (e.g. 20, 30)")] decimal discountPercent,
        [Description("Normal selling price in JPY")] decimal sellingPrice,
        [Description("Cost price of goods in JPY")] decimal costPrice,
        [Description("Promotion end timestamp (ISO 8601)")] string endTimeIso,
        [Description("Batch expiration timestamp (ISO 8601)")] string expiryDateIso,
        [Description("Current stock remaining quantity")] int remainingQuantity,
        [Description("Maximum allowable discount percentage")] decimal maxDiscountPercent)
    {
        var messages = new List<string>();
        bool isValid = true;

        if (!DateTime.TryParse(endTimeIso, out var endTime)) endTime = DateTime.UtcNow.AddHours(5);
        if (!DateTime.TryParse(expiryDateIso, out var expiryDate)) expiryDate = DateTime.UtcNow.AddHours(6);

        // 1. BR-003: Promotion End Time must NOT exceed Batch Expiry Time
        if (endTime > expiryDate)
        {
            isValid = false;
            messages.Add($"[BR-003 違反] プロモーション終了 ({endTime:HH:mm}) が賞味期限 ({expiryDate:HH:mm}) を超えています。");
        }
        else
        {
            messages.Add("[BR-003 適合] プロモーション期間は賞味期限内に収まっています。");
        }

        // 2. BR-006: Inventory Validation (Must have available stock)
        if (remainingQuantity <= 0)
        {
            isValid = false;
            messages.Add("[BR-006 違反] 対象ロットの有効残在庫数が0以下です。");
        }
        else
        {
            messages.Add($"[BR-006 適合] 有効在庫あり（{remainingQuantity}個）。");
        }

        // 3. Max Discount Constraint
        if (discountPercent > maxDiscountPercent)
        {
            isValid = false;
            messages.Add($"[割引上限 違反] 提案割引率 ({discountPercent:F0}%) が商品上限 ({maxDiscountPercent:F0}%) を超過しています。");
        }

        // 4. Minimum Margin Constraint (At least 15% gross profit margin)
        decimal discountedPrice = Math.Round(sellingPrice * (1 - discountPercent / 100m), 0);
        decimal profit = discountedPrice - costPrice;
        decimal profitMarginPct = discountedPrice > 0 ? (profit / discountedPrice) * 100m : 0m;

        if (profitMarginPct < 15.0m)
        {
            isValid = false;
            messages.Add($"[粗利率 警告] 割引後粗利率 ({profitMarginPct:F1}%) が最低利益基準 (15.0%) を下回っています。");
        }
        else
        {
            messages.Add($"[粗利率 クリア] 割引後粗利率 {profitMarginPct:F1}%（粗利 ¥{profit:N0}）を確保。");
        }

        var json = System.Text.Json.JsonSerializer.Serialize(new
        {
            isValid = isValid,
            discountedPrice = discountedPrice,
            profitMargin = profitMarginPct,
            messages = messages
        });

        _logger.LogInformation("[SafetyGuardrailPlugin] Validation result: {IsValid} (Margin: {Margin}%)", isValid, profitMarginPct);
        return json;
    }
}

