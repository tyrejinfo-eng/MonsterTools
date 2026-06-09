using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace MonsterTools.Services
{
    public class LMStudioService
    {
        public string BaseUrl { get; }
        public string ModelName { get; }
        private readonly HttpClient _http;

        public LMStudioService(string baseUrl = "http://127.0.0.1:1234", string modelName = "ibm/granite-4-h-tiny")
        {
            BaseUrl = baseUrl.TrimEnd('/');
            ModelName = modelName;
            _http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        }

        public async Task<string> GetCompletionAsync(string prompt)
        {
            var requestBody = new
            {
                model = ModelName,
                messages = new[] { new { role = "user", content = prompt } },
                stream = false
            };

            var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
            var response = await _http.PostAsync($"{BaseUrl}/chat/completions", content);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadAsStringAsync();
        }
    }
}
