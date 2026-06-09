using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace MonsterTools.Services;

public sealed class LMStudioService
{
    private readonly HttpClient _http;

    public string BaseUrl { get; }
    public string ModelName { get; }

    public LMStudioService(
        string baseUrl = "http://127.0.0.1:1234",
        string modelName = "granite")
    {
        BaseUrl = baseUrl.TrimEnd('/');
        ModelName = modelName;

        _http = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(5)
        };
    }

    public async Task<string> AskAsync(
        string systemPrompt,
        string userPrompt,
        double temperature = 0.1)
    {
        var payload = new
        {
            model = ModelName,
            temperature,
            messages = new object[]
            {
                new
                {
                    role = "system",
                    content = systemPrompt
                },
                new
                {
                    role = "user",
                    content = userPrompt
                }
            }
        };

        var json = JsonSerializer.Serialize(payload);

        using var content =
            new StringContent(
                json,
                Encoding.UTF8,
                "application/json");

        var response =
            await _http.PostAsync(
                $"{BaseUrl}/v1/chat/completions",
                content);

        response.EnsureSuccessStatusCode();

        var result =
            await response.Content.ReadAsStringAsync();

        using var doc =
            JsonDocument.Parse(result);

        return doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString()
            ?? string.Empty;
    }

public async Task<bool> HealthCheckAsync()
{
    try
    {
        using var response =
            await _http.GetAsync(
                $"{BaseUrl}/v1/models");

        return response.IsSuccessStatusCode;
    }
    catch
    {
        return false;
    }
}

public async Task<string> Ask(
    string prompt)
{
    return await AskAsync(
        "You are MonsterTools.",
        prompt,
        0.1);
}

}