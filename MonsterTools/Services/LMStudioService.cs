// MonsterTools/Services/LMStudioService.cs
using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace MonsterTools.Services
{
    public class LMStudioService
    {
        private readonly HttpClient _networkClient;
        private readonly string _targetModelEndpoint;

        // Injected client structure allows safe intercept testing and shared loopback contexts
        public LMStudioService(HttpClient networkClient, string targetModelEndpoint = "http://127.0.0.1:1234")
        {
            _networkClient = networkClient ?? throw new ArgumentNullException(nameof(networkClient));
            _targetModelEndpoint = targetModelEndpoint;
        }

        public async Task<string> QueryModelAsync(string structuredPromptPayload)
        {
            var serializedContent = new StringContent(
                JsonSerializer.Serialize(new { model = "ibm/granite-4-h-tiny", prompt = structuredPromptPayload }),
                Encoding.UTF8,
                "application/json"
            );

            // Re-use system connection contexts to prevent socket exhaustion bugs
            var networkResponse = await _networkClient.PostAsync($"{_targetModelEndpoint}/v1/completions", serializedContent);
            networkResponse.EnsureSuccessStatusCode();

            return await networkResponse.Content.ReadAsStringAsync();
        }
    }
}
