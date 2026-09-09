using RetailRescueAI.Backend.DTOs;

namespace RetailRescueAI.Backend.Services.Interfaces;

public interface IPromotionService
{
    Task<List<PromotionDto>> GetPromotionsAsync(string? status, CancellationToken cancellationToken = default);
    Task<PromotionDto> CreatePromotionAsync(CreatePromotionRequest request, CancellationToken cancellationToken = default);
    Task<bool> ApprovePromotionAsync(int id, CancellationToken cancellationToken = default);
    Task<bool> RejectPromotionAsync(int id, string reason, CancellationToken cancellationToken = default);
}

