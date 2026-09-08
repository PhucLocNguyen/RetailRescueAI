using Microsoft.EntityFrameworkCore;
using RetailRescueAI.Backend.Data;
using RetailRescueAI.Backend.DTOs;

namespace RetailRescueAI.Backend.Services;

public class PosPromotionEngine
{
    private readonly AppDbContext _context;
    private readonly ILogger<PosPromotionEngine> _logger;

    public PosPromotionEngine(AppDbContext context, ILogger<PosPromotionEngine> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<PosRecommendationResponse> GetRecommendationsForCartAsync(
        PosRecommendationRequest request,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        // BR-005: ONLY APPROVED promotions that are currently active in time window
        var activePromotions = await _context.Promotions
            .Include(p => p.TargetProduct)
            .Include(p => p.Conditions)
            .Where(p => p.Status == "APPROVED" && p.StartTime <= now && p.EndTime >= now)
            .ToListAsync(cancellationToken);

        var recommendations = new List<PosRecommendationItemDto>();

        foreach (var promo in activePromotions)
        {
            // Case 1: DIRECT_DISCOUNT
            if (promo.PromotionType == "DIRECT_DISCOUNT" && promo.TargetProductId.HasValue)
            {
                var targetProduct = promo.TargetProduct;
                if (targetProduct != null)
                {
                    decimal discountPct = promo.DiscountPercent ?? 0m;
                    decimal originalPrice = targetProduct.Price;
                    decimal finalPrice = originalPrice * (1.0m - discountPct / 100.0m);

                    // If product is in cart, notify that discount is active!
                    if (request.ProductIdsInCart.Contains(targetProduct.Id))
                    {
                        recommendations.Add(new PosRecommendationItemDto(
                            PromotionId: promo.Id,
                            PromotionCode: promo.PromotionCode,
                            PromotionName: promo.Name,
                            PromotionType: promo.PromotionType,
                            TargetProductId: targetProduct.Id,
                            TargetProductName: targetProduct.Name,
                            OriginalPrice: originalPrice,
                            DiscountPercent: discountPct,
                            FinalPrice: finalPrice,
                            Message: $"【直前割適用】{targetProduct.Name} が {discountPct:F0}% OFF です！",
                            ActionPrompt: $"{targetProduct.Name} の割引価格がレジで適用されています。"
                        ));
                    }
                    else
                    {
                        // If not in cart, recommend adding it!
                        recommendations.Add(new PosRecommendationItemDto(
                            PromotionId: promo.Id,
                            PromotionCode: promo.PromotionCode,
                            PromotionName: promo.Name,
                            PromotionType: promo.PromotionType,
                            TargetProductId: targetProduct.Id,
                            TargetProductName: targetProduct.Name,
                            OriginalPrice: originalPrice,
                            DiscountPercent: discountPct,
                            FinalPrice: finalPrice,
                            Message: $"💡 本日の特売：{targetProduct.Name} が {discountPct:F0}% OFF！",
                            ActionPrompt: $"「{targetProduct.Name}が現在20%引きでお買い得です。いかがでしょうか？」"
                        ));
                    }
                }
            }
            // Case 2: BUY_X_GET_DISCOUNT
            else if (promo.PromotionType == "BUY_X_GET_DISCOUNT")
            {
                var condition = promo.Conditions.FirstOrDefault(c => c.ConditionType == "REQUIRED_PRODUCT");
                if (condition != null && condition.RequiredProductId.HasValue && promo.TargetProductId.HasValue)
                {
                    var hasRequiredProduct = request.ProductIdsInCart.Contains(condition.RequiredProductId.Value);
                    if (hasRequiredProduct)
                    {
                        var targetProduct = promo.TargetProduct;
                        if (targetProduct != null)
                        {
                            decimal discountPct = promo.DiscountPercent ?? 20m;
                            decimal originalPrice = targetProduct.Price;
                            decimal finalPrice = originalPrice * (1.0m - discountPct / 100.0m);

                            recommendations.Add(new PosRecommendationItemDto(
                                PromotionId: promo.Id,
                                PromotionCode: promo.PromotionCode,
                                PromotionName: promo.Name,
                                PromotionType: promo.PromotionType,
                                TargetProductId: targetProduct.Id,
                                TargetProductName: targetProduct.Name,
                                OriginalPrice: originalPrice,
                                DiscountPercent: discountPct,
                                FinalPrice: finalPrice,
                                Message: $"💡 お弁当＋サラダ割引：{targetProduct.Name} を追加すると {discountPct:F0}% OFF！",
                                ActionPrompt: $"「お弁当をお買い上げのお客様に、{targetProduct.Name}が20%OFFになります。ご一緒にいかがでしょうか？」"
                            ));
                        }
                    }
                }
            }
        }

        return new PosRecommendationResponse(recommendations);
    }
}

