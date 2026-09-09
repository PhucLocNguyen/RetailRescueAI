using RetailRescueAI.Backend.DTOs;
using RetailRescueAI.Backend.Models;
using RetailRescueAI.Backend.Repositories.Interfaces;
using RetailRescueAI.Backend.Services.Interfaces;

namespace RetailRescueAI.Backend.Services.Implementations;

public class ProductService : IProductService
{
    private readonly IProductRepository _productRepository;

    public ProductService(IProductRepository productRepository)
    {
        _productRepository = productRepository;
    }

    public async Task<List<PosProductDto>> GetProductsAsync(string? searchQuery, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var products = await _productRepository.SearchProductsAsync(searchQuery, cancellationToken);

        return products.Select(p =>
        {
            var activeBatches = p.Batches.Where(b => b.RemainingQuantity > 0).OrderBy(b => b.ExpiryDate).ToList();
            var totalStock = activeBatches.Sum(b => b.RemainingQuantity);
            var earliestExpiry = activeBatches.FirstOrDefault()?.ExpiryDate;

            string expiryFormatted = earliestExpiry.HasValue
                ? $"{earliestExpiry.Value:yyyy/MM/dd HH:mm} (残り{(earliestExpiry.Value - now).TotalHours:F0}時間)"
                : "在庫なし";

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
                expiryFormatted
            );
        }).ToList();
    }

    public async Task<List<ProductCategory>> GetCategoriesAsync(CancellationToken cancellationToken = default)
    {
        return await _productRepository.GetCategoriesAsync(cancellationToken);
    }
}

