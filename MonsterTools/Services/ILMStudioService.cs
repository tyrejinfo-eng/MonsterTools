using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace MonsterTools.Services
{
    public class LMStudioService
    {
        private readonly HttpClient _httpClient;
        private const string LocalEndpoint = "http://localhost:1234/v1/chat/completions";

        public LMStudioService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<string> GetConstrainedDecisionAsync(string structuralPrompt, string executionTask)
        {
            // Injecting a hyper-strict framing context to minimize small-footprint LLM hallucination
            var payload = new
            {
                model = "ibm/granite-4-h-tiny",
                messages = new[] {
                    new { role = "system", content = "You are an automated router. You must output a valid raw JSON object matching the requested schema definition. Do not wrap in markdown blocks." },
                    new { role = "user", content = $"{structuralPrompt}\n\nTask context to process: {executionTask}" }
                },
                temperature = 0.0, // Hard zero out temperature to keep tools deterministic
                response_format = new { type = "json_object" } // Hard force LM Studio JSON mode execution
            };

            var jsonPayload = JsonSerializer.Serialize(payload);
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(LocalEndpoint, content);
            response.EnsureSuccessStatusCode();

            var responseString = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(responseString);
            
            // Extract content from standard chat completions object structure
            return doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();
        }
    }
}
