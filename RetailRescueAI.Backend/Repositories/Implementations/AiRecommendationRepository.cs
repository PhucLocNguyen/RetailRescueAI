using Microsoft.EntityFrameworkCore;
using RetailRescueAI.Backend.Data;
using RetailRescueAI.Backend.Models;
using RetailRescueAI.Backend.Repositories.Interfaces;

namespace RetailRescueAI.Backend.Repositories.Implementations;

public class AiRecommendationRepository : Repository<AIRecommendation>, IAiRecommendationRepository
{
    public AiRecommendationRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<List<AIRecommendation>> GetAllWithDetailsAsync(CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(r => r.TargetProduct)
            .Include(r => r.TargetBatch)
            .Include(r => r.Evidences)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<AIRecommendation?> GetWithDetailsAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(r => r.TargetProduct)
            .Include(r => r.TargetBatch)
            .Include(r => r.Evidences)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    public async Task<AIRecommendation?> GetPendingForBatchAsync(int batchId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .FirstOrDefaultAsync(r => r.TargetBatchId == batchId && r.Status == "PENDING", cancellationToken);
    }

    public async Task<int> GetPendingCountAsync(CancellationToken cancellationToken = default)
    {
        return await _dbSet.CountAsync(r => r.Status == "PENDING", cancellationToken);
    }
}

