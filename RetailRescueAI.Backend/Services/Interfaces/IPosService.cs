using RetailRescueAI.Backend.DTOs;
using RetailRescueAI.Backend.Models;

namespace RetailRescueAI.Backend.Services.Interfaces;

public interface IPosService
{
    Task<PosRecommendationResponse> GetRecommendationsForCartAsync(PosRecommendationRequest request, CancellationToken cancellationToken = default);
    Task<CheckoutResponse> CheckoutAsync(CheckoutRequest request, CancellationToken cancellationToken = default);
    Task<List<Customer>> GetCustomersAsync(CancellationToken cancellationToken = default);
}

