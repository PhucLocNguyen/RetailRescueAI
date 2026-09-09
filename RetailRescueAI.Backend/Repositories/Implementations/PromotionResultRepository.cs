using Microsoft.EntityFrameworkCore;
using RetailRescueAI.Backend.Data;
using RetailRescueAI.Backend.Models;
using RetailRescueAI.Backend.Repositories.Interfaces;

namespace RetailRescueAI.Backend.Repositories.Implementations;

public class PromotionResultRepository : Repository<PromotionResult>, IPromotionResultRepository
{
    public PromotionResultRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<List<PromotionResult>> GetAllResultsWithDetailsAsync(CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(r => r.Promotion)
            .Include(r => r.Product)
            .Include(r => r.Batch)
            .OrderByDescending(r => r.EvaluatedAt)
            .ToListAsync(cancellationToken);
    }
}

