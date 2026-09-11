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

    public GeminiLLMService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<GeminiLLMService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    public bool IsConfigured
    {
        get
        {
            var apiKey = _configuration["Gemini:ApiKey"];
            return !string.IsNullOrWhiteSpace(apiKey) && apiKey != "YOUR_GEMINI_API_KEY_HERE" && apiKey != "YOUR_GEMINI_API_KEY";
        }
    }

    public async Task<string> GenerateTextAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            _logger.LogError("Gemini API key is not configured.");
            throw new InvalidOperationException("Gemini APIキーが設定されていないか無効です。appsettings.jsonのGemini:ApiKeyを設定してください。");
        }

        var apiKey = _configuration["Gemini:ApiKey"];
        var model = _configuration["Gemini:Model"] ?? "gemini-2.5-flash";
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
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        request.Headers.TryAddWithoutValidation("x-goog-api-key", apiKey);

        var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Gemini GenerateText API failed with status {Status}: {Error}", response.StatusCode, err);
            throw new HttpRequestException($"Gemini APIエラー (HTTP {response.StatusCode}): {err}");
        }

        var resJson = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(resJson);

        if (!doc.RootElement.TryGetProperty("candidates", out var candidates) || candidates.GetArrayLength() == 0)
        {
            throw new InvalidOperationException("Gemini APIから有効な応答候補を取得できませんでした。");
        }

        var text = candidates[0]
            .GetProperty("content")
            .GetProperty("parts")[0]
            .GetProperty("text")
            .GetString();

        if (string.IsNullOrWhiteSpace(text))
        {
            throw new InvalidOperationException("Gemini APIからテキスト応答を取得できませんでした。");
        }

        return text;
    }

    public async Task<string> ChatAsync(string systemPrompt, List<ChatMessageDto> history, string userMessage, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            _logger.LogError("Gemini API key is not configured.");
            throw new InvalidOperationException("Gemini APIキーが設定されていないか無効です。appsettings.jsonのGemini:ApiKeyを設定してください。");
        }

        var apiKey = _configuration["Gemini:ApiKey"];
        var model = _configuration["Gemini:Model"] ?? "gemini-2.5-flash";
        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey}";

        var contents = new List<object>();

        foreach (var h in history.TakeLast(6))
        {
            contents.Add(new
            {
                role = h.Role == "assistant" ? "model" : "user",
                parts = new[] { new { text = h.Content } }
            });
        }

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
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        request.Headers.TryAddWithoutValidation("x-goog-api-key", apiKey);

        var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Gemini Chat API failed with status {Status}: {Error}", response.StatusCode, err);
            throw new HttpRequestException($"Gemini APIエラー (HTTP {response.StatusCode}): {err}");
        }

        var resJson = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(resJson);

        if (!doc.RootElement.TryGetProperty("candidates", out var candidates) || candidates.GetArrayLength() == 0)
        {
            throw new InvalidOperationException("Gemini APIから有効な応答候補を取得できませんでした。");
        }

        var text = candidates[0]
            .GetProperty("content")
            .GetProperty("parts")[0]
            .GetProperty("text")
            .GetString();

        if (string.IsNullOrWhiteSpace(text))
        {
            throw new InvalidOperationException("Gemini APIからチャット応答を取得できませんでした。");
        }

        return text;
    }
}
