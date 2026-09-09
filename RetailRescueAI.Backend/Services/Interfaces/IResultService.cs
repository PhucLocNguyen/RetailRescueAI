using RetailRescueAI.Backend.DTOs;

namespace RetailRescueAI.Backend.Services.Interfaces;

public interface IResultService
{
    Task<DashboardStatsDto> GetDashboardStatsAsync(CancellationToken cancellationToken = default);
    Task<List<PromotionResultDto>> GetPromotionResultsAsync(CancellationToken cancellationToken = default);
}

