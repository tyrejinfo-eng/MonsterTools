using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace MonsterTools.Services
{
    /// <summary>
    /// Implements resilient, non-blocking communications targeting the local LM Studio runtime.
    /// </summary>
    public class LMStudioService
    {
        private readonly HttpClient _httpClient;
        private readonly string _modelEndpoint;
        private readonly string _targetModelName;
        private readonly JsonSerializerOptions _jsonOptions;

        /// <summary>
        /// Instantiates the LLM client by injecting a managed HttpClient lifecycle context.
        /// </summary>
        public LMStudioService(HttpClient httpClient, string modelEndpoint = "http://127.0.0.1:1234", string targetModelName = "ibm/granite-4-h-tiny")
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _modelEndpoint = modelEndpoint.TrimEnd('/');
            _targetModelName = targetModelName;
            
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };
        }

        /// <summary>
        /// Executes an asynchronous chat completion cycle with complete error boundaries.
        /// </summary>
        public async Task<string> QueryModelAsync(string prompt, double temperature = 0.2)
        {
            if (string.IsNullOrWhiteSpace(prompt))
                throw new ArgumentException("The execution prompt cannot be empty or null.", nameof(prompt));

            try
            {
                // Construct standard OpenAI compatible chat completion schema payload
                var payload = new ChatCompletionRequest
                {
                    Model = _targetModelName,
                    Messages = new List<ChatMessage>
                    {
                        new() { Role = "system", Content = "You are a deterministic tool orchestration engine. Respond precisely with tool invocations only." },
                        new() { Role = "user", Content = prompt }
                    },
                    Temperature = temperature,
                    Stream = false
                };

                var jsonPayload = JsonSerializer.Serialize(payload, _jsonOptions);
                using var requestContent = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                // Execute via the shared HttpClient lifecycle wrapper to prevent socket exhaustion
                var response = await _httpClient.PostAsync($"{_modelEndpoint}/v1/chat/completions", requestContent);
                
                if (!response.IsSuccessStatusCode)
                {
                    var partialErrorPayload = await response.Content.ReadAsStringAsync();
                    return $"Error: Downstream engine returned status {response.StatusCode}. Details: {partialErrorPayload}";
                }

                var responseJson = await response.Content.ReadAsStringAsync();
                var completionResult = JsonSerializer.Deserialize<ChatCompletionResponse>(responseJson, _jsonOptions);

                return completionResult?.Choices?[0]?.Message?.Content ?? "Error: Empty response string received from local model.";
            }
            catch (HttpRequestException httpEx)
            {
                // Isolate network-level connectivity failures from the core orchestration loop
                return $"Error: Communication link with local model host failed. Target: {_modelEndpoint}. Issue: {httpEx.Message}";
            }
            catch (JsonException jsonEx)
            {
                // Catch schema drift or partial syntax payloads safely
                return $"Error: Failed to process local model response schema formatting. Details: {jsonEx.Message}";
            }
            catch (Exception ex)
            {
                return $"Error: An unexpected runtime exception occurred. Details: {ex.Message}";
            }
        }
    }

    #region Internal Schema Definitions
    
    public class ChatCompletionRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; set; } = string.Empty;

        [JsonPropertyName("messages")]
        public List<ChatMessage> Messages { get; set; } = new();

        [JsonPropertyName("temperature")]
        public double Temperature { get; set; } = 0.2;

        [JsonPropertyName("stream")]
        public bool Stream { get; set; } = false;
    }

    public class ChatMessage
    {
        [JsonPropertyName("role")]
        public string Role { get; set; } = string.Empty;

        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;
    }

    public class ChatCompletionResponse
    {
        [JsonPropertyName("choices")]
        public List<ChatChoice>? Choices { get; set; }
    }

    public class ChatChoice
    {
        [JsonPropertyName("message")]
        public ChatMessage? Message { get; set; }
    }

    #endregion
}
