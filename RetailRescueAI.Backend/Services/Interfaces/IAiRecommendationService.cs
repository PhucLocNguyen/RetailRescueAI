using RetailRescueAI.Backend.DTOs;

namespace RetailRescueAI.Backend.Services.Interfaces;

public interface IAiRecommendationService
{
    Task<List<AIRecommendationDto>> GetRecommendationsAsync(CancellationToken cancellationToken = default);
    Task<(bool Success, string Message, int? PromotionId)> ApproveRecommendationAsync(int id, ApproveRecommendationRequest? request, CancellationToken cancellationToken = default);
    Task<bool> RejectRecommendationAsync(int id, RejectRecommendationRequest request, CancellationToken cancellationToken = default);
    Task<AiPipelineRunResponse> RunManualPipelineAsync(CancellationToken cancellationToken = default);
}

