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
            .Include(r => r.ComboProduct)
            .Include(r => r.Evidences)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<AIRecommendation>> GetPendingRecommendationsAsync(CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(r => r.TargetProduct)
            .Include(r => r.TargetBatch)
            .Include(r => r.ComboProduct)
            .Include(r => r.Evidences)
            .Where(r => r.Status == "PENDING")
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<AIRecommendation?> GetWithDetailsAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(r => r.TargetProduct)
            .Include(r => r.TargetBatch)
            .Include(r => r.ComboProduct)
            .Include(r => r.Evidences)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    public async Task<AIRecommendation?> GetPendingForBatchAsync(int batchId, string? recommendationType = null, CancellationToken cancellationToken = default)
    {
        var q = _dbSet.Where(r => r.TargetBatchId == batchId && r.Status == "PENDING");
        if (!string.IsNullOrEmpty(recommendationType))
        {
            q = q.Where(r => r.RecommendationType == recommendationType);
        }
        return await q.FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<int> GetPendingCountAsync(CancellationToken cancellationToken = default)
    {
        return await _dbSet.CountAsync(r => r.Status == "PENDING", cancellationToken);
    }
}

