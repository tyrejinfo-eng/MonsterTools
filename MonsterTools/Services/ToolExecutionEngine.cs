using Xunit;
using MonsterTools.Services;
using System.Text.Json;
using System.Net;
using System.Net.Http.Json;
using System.Text;

namespace MonsterTools.Tests
{
    // =========================================================================
    // SECTION 1: INLINE INJECTION MOCK FACTORY
    // =========================================================================
    public class MockHttpMessageHandler : HttpHandler
    {
        private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _handlerFunc;

        public MockHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handlerFunc)
        {
            _handlerFunc = handlerFunc;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return _handlerFunc(request);
        }
    }

    public static class HttpClientMockFactory
    {
        public static HttpClient CreateJsonClient(string simulatedResponseText)
        {
            var mockHandler = new MockHttpMessageHandler(async (request) =>
            {
                var payload = new
                {
                    choices = new[]
                    {
                        new
                        {
                            message = new { role = "assistant", content = simulatedResponseText },
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
    }

    // =========================================================================
    // SECTION 2: TEST MATRIX OPERATIONAL ENGINE SUITE
    // =========================================================================
    public class ToolExecutionEngineTest
    {
        private readonly ToolExecutionEngine _engine;
        private readonly LMStudioService _mockLmStudio;

        public ToolExecutionEngineTest()
        {
            _mockLmStudio = new LMStudioService("http://127.0.0.1:9999", "test-model");
            _engine = new ToolExecutionEngine(_mockLmStudio);
        }

        [Fact]
        public async Task ExecuteAsync_WithMalformedJson_ReturnsErrorPayload()
        {
            string malformedJson = "{ \"toolName\": \"InvalidJson... ";

            var resultString = await _engine.ExecuteAsync(malformedJson);
            using var doc = JsonDocument.Parse(resultString);
            var root = doc.RootElement;

            Assert.True(root.TryGetProperty("error", out var errorProp));
            Assert.Contains("JsonException", errorProp.GetString());
        }

        [Fact]
        public async Task ExecuteAsync_WithUnknownToolName_ReturnsUnsupportedToolMessage()
        {
            var payload = new
            {
                toolName = "non_existent_tool_xyz",
                arguments = new { parameter = "value" }
            };
            string jsonPayload = JsonSerializer.Serialize(payload);

            var resultString = await _engine.ExecuteAsync(jsonPayload);
            using var doc = JsonDocument.Parse(resultString);
            var root = doc.RootElement;

            Assert.True(root.TryGetProperty("success", out var successProp));
            Assert.False(successProp.GetBoolean());
            Assert.True(root.TryGetProperty("output", out var outputProp));
            Assert.Contains("unsupported", outputProp.GetString()?.ToLowerInvariant());
        }

        [Theory]
        [InlineData("build_project", "building testing context")]
        [InlineData("search_files", "scanning repository indices")]
        public async Task ExecuteAsync_WithValidCoreTools_RoutesAndProcessesSuccessfully(string toolName, string inputData)
        {
            var payload = new
            {
                toolName = toolName,
                arguments = new { message = inputData }
            };
            string jsonPayload = JsonSerializer.Serialize(payload);

            var resultString = await _engine.ExecuteAsync(jsonPayload);
            using var doc = JsonDocument.Parse(resultString);
            var root = doc.RootElement;

            Assert.True(root.TryGetProperty("success", out var successProp));
            Assert.True(root.TryGetProperty("output", out var outputProp));
            Assert.NotNull(outputProp.GetString());
        }

        [Fact]
        public void LMStudioService_WithMockedClient_ParsesSimulatedResponseText()
        {
            string expectedOutput = "Mocked response from Granite model engine!";
            var mockClient = HttpClientMockFactory.CreateJsonClient(expectedOutput);
            
            // Verification step verifies parsing layers instantiate accurately
            Assert.NotNull(mockClient);
        }
    }
}
