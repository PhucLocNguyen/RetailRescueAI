using RetailRescueAI.Backend.DTOs;
using RetailRescueAI.Backend.Models;
using RetailRescueAI.Backend.Repositories.Interfaces;
using RetailRescueAI.Backend.Services.Interfaces;

namespace RetailRescueAI.Backend.Services.Implementations;

public class ProductService : IProductService
{
    private readonly IProductRepository _productRepository;
    private readonly IPromotionRepository _promotionRepository;

    public ProductService(IProductRepository productRepository, IPromotionRepository promotionRepository)
    {
        _productRepository = productRepository;
        _promotionRepository = promotionRepository;
    }

    public async Task<List<PosProductDto>> GetProductsAsync(string? searchQuery, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var products = await _productRepository.SearchProductsAsync(searchQuery, cancellationToken);
        var activePromos = await _promotionRepository.GetActivePromotionsAsync(now, cancellationToken);

        return products.Select(p =>
        {
            var batches = p.Batches.Where(b => b.RemainingQuantity > 0).OrderBy(b => b.ExpiryDate).ToList();
            var validBatches = batches.Where(b => b.ExpiryDate > now).ToList();
            var totalStock = validBatches.Sum(b => b.RemainingQuantity);
            var earliestExpiry = validBatches.FirstOrDefault()?.ExpiryDate;

            string expiryFormatted = earliestExpiry.HasValue
                ? $"{earliestExpiry.Value:yyyy/MM/dd HH:mm} (残り{(earliestExpiry.Value - now).TotalHours:F0}時間)"
                : (batches.Any() ? "期限切れ商品のみ" : "在庫なし");

            var batchSummaries = batches.Select(b =>
            {
                bool isExpired = b.ExpiryDate <= now;
                var promo = activePromos.FirstOrDefault(pr => pr.TargetBatchId == b.Id);
                bool isDiscounted = promo != null && promo.DiscountPercent.HasValue && !isExpired;
                decimal? discountPercent = isDiscounted ? promo?.DiscountPercent : null;
                decimal? finalPrice = isDiscounted && discountPercent.HasValue
                    ? Math.Round(p.Price * (1.0m - discountPercent.Value / 100.0m), 0)
                    : p.Price;

                double hoursLeft = (b.ExpiryDate - now).TotalHours;
                string bExpiryFmt = isExpired
                    ? $"期限切れ ({b.ExpiryDate:MM/dd HH:mm})"
                    : $"{b.ExpiryDate:MM/dd HH:mm} (残り{hoursLeft:F0}時間)";

                return new PosBatchSummaryDto(
                    Id: b.Id,
                    BatchCode: b.BatchCode,
                    ProductId: p.Id,
                    RemainingQuantity: b.RemainingQuantity,
                    ProductionDate: b.ProductionDate,
                    ExpiryDate: b.ExpiryDate,
                    HoursUntilExpiry: hoursLeft,
                    ExpiryFormatted: bExpiryFmt,
                    IsExpired: isExpired,
                    IsDiscounted: isDiscounted,
                    DiscountPercent: discountPercent,
                    FinalPrice: finalPrice,
                    PromotionId: isDiscounted ? promo?.Id : null,
                    PromotionName: isDiscounted ? promo?.Name : null
                );
            }).ToList();

            return new PosProductDto(
                p.Id,
                p.ProductCode,
                p.Name,
                p.Description,
                p.Category?.Name ?? "その他",
                p.Price,
                p.Barcode,
                p.ImageUrl,
                totalStock,
                expiryFormatted,
                batchSummaries
            );
        }).ToList();
    }

    public async Task<List<ProductCategory>> GetCategoriesAsync(CancellationToken cancellationToken = default)
    {
        return await _productRepository.GetCategoriesAsync(cancellationToken);
    }
}

