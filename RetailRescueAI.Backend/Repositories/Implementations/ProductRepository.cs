using Microsoft.EntityFrameworkCore;
using RetailRescueAI.Backend.Data;
using RetailRescueAI.Backend.Models;
using RetailRescueAI.Backend.Repositories.Interfaces;

namespace RetailRescueAI.Backend.Repositories.Implementations;

public class ProductRepository : Repository<Product>, IProductRepository
{
    public ProductRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<List<Product>> SearchProductsAsync(string? query, CancellationToken cancellationToken = default)
    {
        var q = _dbSet
            .Include(p => p.Category)
            .Include(p => p.Batches)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query))
        {
            var search = query.Trim().ToLower();
            q = q.Where(p =>
                p.Name.ToLower().Contains(search) ||
                p.ProductCode.ToLower().Contains(search) ||
                p.Barcode.Contains(search));
        }

        return await q.ToListAsync(cancellationToken);
    }

    public async Task<List<ProductCategory>> GetCategoriesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.ProductCategories.ToListAsync(cancellationToken);
    }

    public async Task<Product?> GetProductWithBatchesAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(p => p.Category)
            .Include(p => p.Batches)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }
}

