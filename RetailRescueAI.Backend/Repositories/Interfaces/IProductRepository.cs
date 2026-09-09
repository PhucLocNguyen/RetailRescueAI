using RetailRescueAI.Backend.Models;

namespace RetailRescueAI.Backend.Repositories.Interfaces;

public interface IProductRepository : IRepository<Product>
{
    Task<List<Product>> SearchProductsAsync(string? query, CancellationToken cancellationToken = default);
    Task<List<ProductCategory>> GetCategoriesAsync(CancellationToken cancellationToken = default);
    Task<Product?> GetProductWithBatchesAsync(int id, CancellationToken cancellationToken = default);
}

