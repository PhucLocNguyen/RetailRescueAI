using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using RetailRescueAI.Backend.DTOs;

namespace RetailRescueAI.Backend.Services.AI;

public class GeminiLLMService : ILLMService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<GeminiLLMService> _logger;
    private readonly MockLLMService _fallbackMockService;

    public GeminiLLMService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<GeminiLLMService> logger,
        MockLLMService fallbackMockService)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
        _fallbackMockService = fallbackMockService;
    }

    public bool IsConfigured
    {
        get
        {
            var apiKey = _configuration["Gemini:ApiKey"];
            return !string.IsNullOrWhiteSpace(apiKey) && apiKey != "YOUR_GEMINI_API_KEY_HERE";
        }
    }

    public async Task<string> GenerateTextAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            _logger.LogInformation("Gemini API key is not configured. Using MockLLMService fallback.");
            return await _fallbackMockService.GenerateTextAsync(systemPrompt, userPrompt, cancellationToken);
        }

        try
        {
            var apiKey = _configuration["Gemini:ApiKey"];
            var model = _configuration["Gemini:Model"] ?? "gemini-1.5-flash";
            var url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey}";

            var requestPayload = new
            {
                contents = new[]
                {
                    new
                    {
                        role = "user",
                        parts = new[] { new { text = userPrompt } }
                    }
                },
                systemInstruction = new
                {
                    parts = new[] { new { text = systemPrompt } }
                }
            };

            var json = JsonSerializer.Serialize(requestPayload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(url, content, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var err = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("Gemini API call failed with status {Status}: {Error}. Falling back to Mock.", response.StatusCode, err);
                return await _fallbackMockService.GenerateTextAsync(systemPrompt, userPrompt, cancellationToken);
            }

            var resJson = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(resJson);
            var text = doc.RootElement
                .GetProperty("candidates")[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text")
                .GetString();

            return text ?? await _fallbackMockService.GenerateTextAsync(systemPrompt, userPrompt, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception calling Gemini API. Falling back to Mock.");
            return await _fallbackMockService.GenerateTextAsync(systemPrompt, userPrompt, cancellationToken);
        }
    }

    public async Task<string> ChatAsync(string systemPrompt, List<ChatMessageDto> history, string userMessage, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            return await _fallbackMockService.ChatAsync(systemPrompt, history, userMessage, cancellationToken);
        }

        try
        {
            var apiKey = _configuration["Gemini:ApiKey"];
            var model = _configuration["Gemini:Model"] ?? "gemini-1.5-flash";
            var url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey}";

            var contents = new List<object>();

            // Add history
            foreach (var h in history.TakeLast(6))
            {
                contents.Add(new
                {
                    role = h.Role == "assistant" ? "model" : "user",
                    parts = new[] { new { text = h.Content } }
                });
            }

            // Add current message
            contents.Add(new
            {
                role = "user",
                parts = new[] { new { text = userMessage } }
            });

            var requestPayload = new
            {
                contents = contents,
                systemInstruction = new
                {
                    parts = new[] { new { text = systemPrompt } }
                }
            };

            var json = JsonSerializer.Serialize(requestPayload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(url, content, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return await _fallbackMockService.ChatAsync(systemPrompt, history, userMessage, cancellationToken);
            }

            var resJson = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(resJson);
            var text = doc.RootElement
                .GetProperty("candidates")[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text")
                .GetString();

            return text ?? await _fallbackMockService.ChatAsync(systemPrompt, history, userMessage, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception in Gemini ChatAsync. Falling back to Mock.");
            return await _fallbackMockService.ChatAsync(systemPrompt, history, userMessage, cancellationToken);
        }
    }
}

