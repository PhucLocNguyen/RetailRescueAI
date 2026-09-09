using RetailRescueAI.Backend.DTOs;

namespace RetailRescueAI.Backend.Services.Interfaces;

public interface IAuthService
{
    Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
}

