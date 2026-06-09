using System.Net;
using System.Net.Http.Json;
using System.Text;

namespace MonsterTools.Tests
{
    /// <summary>
    /// Custom mock handler to intercept and simulate responses from LM Studio.
    /// </summary>
    public class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _handlerFunc;

        public MockHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handlerFunc)
        {
            _handlerFunc = handlerFunc;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, 
            CancellationToken cancellationToken)
        {
            return _handlerFunc(request);
        }
    }

    /// <summary>
    /// Factory to construct custom instances of HttpClient pre-configured with mocked responses.
    /// </summary>
    public static class HttpClientMockFactory
    {
        /// <summary>
        /// Creates an HttpClient that yields a standard non-streaming JSON response payload.
        /// </summary>
        public static HttpClient CreateJsonClient(string simulatedResponseText)
        {
            var mockHandler = new MockHttpMessageHandler(async (request) =>
            {
                // Shape an OpenAI / LM Studio compatible Chat Completion JSON block
                var payload = new
                {
                    choices = new[]
                    {
                        new
                        {
                            message = new
                            {
                                role = "assistant",
                                content = simulatedResponseText
                            },
                            finish_reason = "stop"
                        }
                    }
                };

                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(payload)
                };

                return await Task.FromResult(response);
            });

            return new HttpClient(mockHandler);
        }

        /// <summary>
        /// Creates an HttpClient that simulates a Server-Sent Events (SSE) live data stream.
        /// </summary>
        public static HttpClient CreateStreamingClient(IEnumerable<string> continuousTextTokens)
        {
            var mockHandler = new MockHttpMessageHandler(async (request) =>
            {
                var sseBuilder = new StringBuilder();

                foreach (var token in continuousTextTokens)
                {
                    var chunkPayload = new
                    {
                        choices = new[]
                        {
                            new
                            {
                                delta = new { content = token }
                            }
                        }
                    };

                    // Format line output exactly matching LM Studio streaming syntax
                    sseBuilder.Append($"data: {System.Text.Json.JsonSerializer.Serialize(chunkPayload)}\n\n");
                }
                
                sseBuilder.Append("data: [DONE]\n\n");

                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(sseBuilder.ToString(), Encoding.UTF8, "text/event-stream")
                };

                return await Task.FromResult(response);
            });

            return new HttpClient(mockHandler);
        }
    }
}
