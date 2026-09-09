using Microsoft.EntityFrameworkCore;
using RetailRescueAI.Backend.Data;
using RetailRescueAI.Backend.Models;
using RetailRescueAI.Backend.Repositories.Interfaces;

namespace RetailRescueAI.Backend.Repositories.Implementations;

public class SaleRepository : Repository<Sale>, ISaleRepository
{
    public SaleRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<List<SaleItem>> GetRecentSaleItemsAsync(DateTime since, CancellationToken cancellationToken = default)
    {
        return await _context.SaleItems
            .Include(si => si.Sale)
            .Where(si => si.Sale!.CreatedAt >= since && si.Sale.Status == "COMPLETED")
            .ToListAsync(cancellationToken);
    }

    public async Task<Sale> CreateSaleTransactionAsync(Sale sale, CancellationToken cancellationToken = default)
    {
        await _dbSet.AddAsync(sale, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return sale;
    }
}

