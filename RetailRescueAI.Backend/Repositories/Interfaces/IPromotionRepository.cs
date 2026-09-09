using RetailRescueAI.Backend.Models;

namespace RetailRescueAI.Backend.Repositories.Interfaces;

public interface IPromotionRepository : IRepository<Promotion>
{
    Task<List<Promotion>> GetPromotionsAsync(string? status, CancellationToken cancellationToken = default);
    Task<List<Promotion>> GetActivePromotionsAsync(DateTime now, CancellationToken cancellationToken = default);
    Task<Promotion?> GetPromotionWithDetailsAsync(int id, CancellationToken cancellationToken = default);
}

