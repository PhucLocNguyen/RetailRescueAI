using RetailRescueAI.Backend.DTOs;

namespace RetailRescueAI.Backend.Services.Interfaces;

public interface IChatbotService
{
    Task<ChatResponse> ProcessChatAsync(ChatRequest request, CancellationToken cancellationToken = default);
}

