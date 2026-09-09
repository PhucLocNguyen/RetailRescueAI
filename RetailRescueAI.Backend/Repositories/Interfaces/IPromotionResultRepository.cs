using RetailRescueAI.Backend.Models;

namespace RetailRescueAI.Backend.Repositories.Interfaces;

public interface IPromotionResultRepository : IRepository<PromotionResult>
{
    Task<List<PromotionResult>> GetAllResultsWithDetailsAsync(CancellationToken cancellationToken = default);
}

