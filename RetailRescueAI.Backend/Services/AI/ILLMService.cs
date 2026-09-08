using RetailRescueAI.Backend.DTOs;

namespace RetailRescueAI.Backend.Services.AI;

public interface ILLMService
{
    bool IsConfigured { get; }
    Task<string> GenerateTextAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken = default);
    Task<string> ChatAsync(string systemPrompt, List<ChatMessageDto> history, string userMessage, CancellationToken cancellationToken = default);
}

