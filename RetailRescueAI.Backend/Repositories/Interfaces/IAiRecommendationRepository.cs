using RetailRescueAI.Backend.Models;

namespace RetailRescueAI.Backend.Repositories.Interfaces;

public interface IAiRecommendationRepository : IRepository<AIRecommendation>
{
    Task<List<AIRecommendation>> GetAllWithDetailsAsync(CancellationToken cancellationToken = default);
    Task<AIRecommendation?> GetWithDetailsAsync(int id, CancellationToken cancellationToken = default);
    Task<AIRecommendation?> GetPendingForBatchAsync(int batchId, CancellationToken cancellationToken = default);
    Task<int> GetPendingCountAsync(CancellationToken cancellationToken = default);
}

