using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using MonsterTools.Core;
using MonsterTools.Services;

namespace MonsterTools
{
    public class MonsterMcpServer
    {
        private readonly ToolRouter _toolRouter;
        private readonly WorkerDispatcher _workerDispatcher;

        public MonsterMcpServer(ToolRouter toolRouter, WorkerDispatcher workerDispatcher)
        {
            _toolRouter = toolRouter;
            _workerDispatcher = workerDispatcher;
        }

        public async Task StartAsync()
        {
            using var reader = new StreamReader(Console.OpenStandardInput());
            using var writer = new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true };

            while (!reader.EndOfStream)
            {
                var line = await reader.ReadLineAsync();
                if (string.IsNullOrWhiteSpace(line)) continue;

                try
                {
                    var jsonDoc = JsonDocument.Parse(line);
                    var root = jsonDoc.RootElement;

                    if (root.TryGetProperty("method", out var methodProp))
                    {
                        string method = methodProp.GetString();
                        var id = root.GetProperty("id").GetRawText();

                        if (method == "tools/list")
                        {
                            var response = GetToolsListResponse(id);
                            await writer.WriteLineAsync(response);
                        }
                        else if (method == "tools/call")
                        {
                            var response = await HandleToolCallAsync(id, root.GetProperty("params"));
                            await writer.WriteLineAsync(response);
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Fallback reporting channel to prevent crashing the stdio pipe stream
                    System.Diagnostics.Debug.WriteLine($"MCP Server Error: {ex.Message}");
                }
            }
        }

        private string GetToolsListResponse(string id)
        {
            var tools = _toolRouter.GetRegisteredSchemas(); // Sourced from ToolSchemas.cs
            var responseObj = new { jsonrpc = "2.0", id = int.Parse(id), result = new { tools = tools } };
            return JsonSerializer.Serialize(responseObj);
        }

        private async Task<string> HandleToolCallAsync(string id, JsonElement paramsElement)
        {
            string toolName = paramsElement.GetProperty("name").GetString();
            var arguments = paramsElement.GetProperty("arguments").GetRawText();

            // Normalize and parse input using existing Layer 3 infrastructure
            var toolRequest = new ToolRequest { ToolName = toolName, RawArguments = arguments };
            var toolResult = await _workerDispatcher.DispatchAsync(toolRequest);

            var responseObj = new {
                jsonrpc = "2.0",
                id = int.Parse(id),
                result = new {
                    content = new[] {
                        new { type = "text", text = toolResult.OutputData }
                    },
                    isError = !toolResult.IsSuccess
                }
            };
            return JsonSerializer.Serialize(responseObj);
        }
    }
}
