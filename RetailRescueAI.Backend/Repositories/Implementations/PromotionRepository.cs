using Microsoft.EntityFrameworkCore;
using RetailRescueAI.Backend.Data;
using RetailRescueAI.Backend.Models;
using RetailRescueAI.Backend.Repositories.Interfaces;

namespace RetailRescueAI.Backend.Repositories.Implementations;

public class PromotionRepository : Repository<Promotion>, IPromotionRepository
{
    public PromotionRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<List<Promotion>> GetPromotionsAsync(string? status, CancellationToken cancellationToken = default)
    {
        var q = _dbSet
            .Include(p => p.TargetProduct)
            .Include(p => p.TargetBatch)
            .Include(p => p.ComboProduct)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
        {
            q = q.Where(p => p.Status == status.ToUpper());
        }

        return await q.OrderByDescending(p => p.CreatedAt).ToListAsync(cancellationToken);
    }

    public async Task<List<Promotion>> GetActivePromotionsAsync(DateTime now, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(p => p.TargetProduct)
            .Include(p => p.TargetBatch)
            .Include(p => p.ComboProduct)
            .Include(p => p.Conditions)
            .Where(p => p.Status == "APPROVED" && p.StartTime <= now && p.EndTime >= now)
            .ToListAsync(cancellationToken);
    }

    public async Task<Promotion?> GetPromotionWithDetailsAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(p => p.TargetProduct)
            .Include(p => p.TargetBatch)
            .Include(p => p.ComboProduct)
            .Include(p => p.Conditions)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }
}

