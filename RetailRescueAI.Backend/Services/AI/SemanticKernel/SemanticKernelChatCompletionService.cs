using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using RetailRescueAI.Backend.DTOs;
using RetailRescueAI.Backend.Services.AI;

namespace RetailRescueAI.Backend.Services.AI.SemanticKernel;

public class SemanticKernelChatCompletionService : IChatCompletionService
{
    private readonly ILLMService _llmService;
    private readonly Dictionary<string, object?> _attributes = new();

    public SemanticKernelChatCompletionService(ILLMService llmService)
    {
        _llmService = llmService;
    }

    public IReadOnlyDictionary<string, object?> Attributes => _attributes;

    public async Task<IReadOnlyList<ChatMessageContent>> GetChatMessageContentsAsync(
        ChatHistory chatHistory,
        PromptExecutionSettings? executionSettings = null,
        Kernel? kernel = null,
        CancellationToken cancellationToken = default)
    {
        var systemMessages = chatHistory
            .Where(m => m.Role == AuthorRole.System)
            .Select(m => m.Content ?? string.Empty)
            .ToList();

        string systemPrompt = systemMessages.Count > 0
            ? string.Join("\n\n", systemMessages)
            : "あなたはスーパーマーケットの在庫管理・プロモーション策定を行う専門AIアシスタントです。";

        var nonSystemMessages = chatHistory
            .Where(m => m.Role != AuthorRole.System)
            .ToList();

        var historyDtos = new List<ChatMessageDto>();
        string userMessage = string.Empty;

        for (int i = 0; i < nonSystemMessages.Count; i++)
        {
            var msg = nonSystemMessages[i];
            bool isLast = (i == nonSystemMessages.Count - 1);

            if (isLast && msg.Role == AuthorRole.User)
            {
                userMessage = msg.Content ?? string.Empty;
            }
            else
            {
                string roleName = msg.Role == AuthorRole.Assistant ? "assistant" : "user";
                historyDtos.Add(new ChatMessageDto(roleName, msg.Content ?? string.Empty, DateTime.UtcNow.AddHours(7)));
                historyDtos.Add(new ChatMessageDto(roleName, msg.Content ?? string.Empty, RetailRescueAI.Backend.Common.AppClock.Now));
            }
        }

        if (string.IsNullOrWhiteSpace(userMessage) && nonSystemMessages.Count > 0)
        {
            userMessage = nonSystemMessages.Last().Content ?? string.Empty;
        }

        string reply = await _llmService.ChatAsync(systemPrompt, historyDtos, userMessage, cancellationToken);

        return new List<ChatMessageContent>
        {
            new(AuthorRole.Assistant, reply)
        };
    }

    public async IAsyncEnumerable<StreamingChatMessageContent> GetStreamingChatMessageContentsAsync(
        ChatHistory chatHistory,
        PromptExecutionSettings? executionSettings = null,
        Kernel? kernel = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var contents = await GetChatMessageContentsAsync(chatHistory, executionSettings, kernel, cancellationToken);
        foreach (var content in contents)
        {
            yield return new StreamingChatMessageContent(AuthorRole.Assistant, content.Content);
        }
    }
}

