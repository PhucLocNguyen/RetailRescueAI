using Microsoft.EntityFrameworkCore;
using RetailRescueAI.Backend.Data;
using RetailRescueAI.Backend.Models;
using RetailRescueAI.Backend.Repositories.Interfaces;

namespace RetailRescueAI.Backend.Repositories.Implementations;

public class InventoryBatchRepository : Repository<InventoryBatch>, IInventoryBatchRepository
{
    public InventoryBatchRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<List<InventoryBatch>> GetAllBatchesWithProductAsync(CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(b => b.Product)
            .ThenInclude(p => p!.Category)
            .OrderBy(b => b.ExpiryDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<InventoryBatch>> GetActiveBatchesAsync(CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(b => b.Product)
            .ThenInclude(p => p!.Category)
            .Where(b => b.RemainingQuantity > 0)
            .OrderBy(b => b.ExpiryDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<InventoryBatch>> GetAvailableBatchesForProductFefoAsync(int productId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(b => b.ProductId == productId && b.RemainingQuantity > 0)
            .OrderBy(b => b.ExpiryDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<InventoryBatch>> GetUrgentBatchesAsync(CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(b => b.Product)
            .Where(b => b.RemainingQuantity > 0 && (b.Status == "CRITICAL" || b.Status == "AT_RISK"))
            .OrderBy(b => b.ExpiryDate)
            .ToListAsync(cancellationToken);
    }
}

