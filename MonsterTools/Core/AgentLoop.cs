using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using MonsterTools.Services;

namespace MonsterTools.Core
{
    public sealed class AgentLoop
    {
        private const int MaxIterations = 10; // Expanded to support deep automated engineering runs
        private readonly ILMStudioService _lmStudio;
        private readonly ToolRouter _toolRouter;
        private readonly ToolArgumentNormalizer _normalizer;
        
        private static string _currentWorkspaceRoot = string.Empty;
        private static bool _workspaceStateStale = false;

        public AgentLoop(
            ILMStudioService lmStudio,
            ToolRouter toolRouter,
            ToolArgumentNormalizer normalizer)
        {
            _lmStudio = lmStudio;
            _toolRouter = toolRouter;
            _normalizer = normalizer;
        }

        /// <summary>
        /// Entry point called directly by the incoming WebSocket command handler inside Program.cs
        /// </summary>
        public void InjectUserInstruction(string instruction)
        {
            if (string.IsNullOrWhiteSpace(_currentWorkspaceRoot))
            {
                Program.PushDirectUiUpdate("System", "Error", 0.0, 0, 0, "[CORE LOG] Active workspace targeting boundary missing. Execute abort.", "sys");
                return;
            }

            // Fire and forget the orchestrator worker loop asynchronously so it doesn't block the networking thread context
            _ = Task.Run(async () => await RunOrchestrationCycleAsync(instruction, _currentWorkspaceRoot).ConfigureAwait(false));
        }

        public static void StartOrchestrator(string workspaceRoot)
        {
            _currentWorkspaceRoot = workspaceRoot;
            _workspaceStateStale = false;
        }

        public static void NotifyWorkspaceMutation()
        {
            _workspaceStateStale = true;
        }

        public static void HandleFileMutationUpdate(string fullPath)
        {
            _workspaceStateStale = true;
            Program.PushDirectUiUpdate("FileSystem", "Index Stale", 0.0, 0, 0, $"[STATE] Workspace mutation registered at: {Path.GetFileName(fullPath)}. Context validation flag elevated.", "sys");
        }

        private async Task RunOrchestrationCycleAsync(string prompt, string workspaceRoot, CancellationToken cancellationToken = default)
        {
            Program.PushDirectUiUpdate("AgentLoop", "Evaluating", 0.0, 0, 32768, "[AGENT] Parsing prompt intent and local state metrics...", "llm");

            var history = new List<ChatMessageContext>
            {
                new()
                {
                    Role = "system",
                    Content = """
                              You are MonsterTools, an air-gapped deterministic coding loop executor.
                              You execute development actions using specialized tool call schemas.
                              
                              When a tool is required, you must return ONLY a clean JSON object using exactly this shape:
                              {
                                "id": "call-1",
                                "type": "function",
                                "function": {
                                  "name": "tool_name",
                                  "arguments": "{ \"key\": \"value\" }"
                                }
                              }
                              
                              Available tool workflows: WorkspaceScanWorker, ReadFileWorker, WriteFileWorker, PatchFileWorker, SearchWorker, AstWorker, ValidationWorker, BuildWorker, CompilerWorker, TestWorker, GitWorker.
                              When the target issue is fully resolved or no tool execution is needed, provide your concise human engineer response text directly without generating JSON.
                              """
                },
                new()
                {
                    Role = "user",
                    Content = JsonSerializer.Serialize(new { prompt, workspaceRoot, stateStale = _workspaceStateStale })
                }
            };

            string lastModelResponse = string.Empty;

            for (var iteration = 0; iteration < MaxIterations; iteration++)
            {
                // Reset stale workspace verification indicators once ingestion commences
                _workspaceStateStale = false;

                // Capture performance diagnostics immediately preceding local inference activation
                var watch = System.Diagnostics.Stopwatch.StartNew();
                
                Program.PushDirectUiUpdate("LocalLLM", "Reasoning", 0.0, history.Count * 250, 32768, $"[LLM] Interrogating local model engine (Iteration {iteration + 1})...", "llm");
                
                lastModelResponse = await _lmStudio.QueryModelHistoryAsync(history, cancellationToken).ConfigureAwait(false);
                
                watch.Stop();
                
                // Approximate performance calculation metric based on output string token generation
                double generatedTokens = lastModelResponse.Length / 4.0;
                double tokensPerSecond = generatedTokens / (watch.ElapsedMilliseconds / 1000.0);
                if (double.IsInfinity(tokensPerSecond) || double.IsNaN(tokensPerSecond)) tokensPerSecond = 35.0;

                if (!TryParseToolCall(lastModelResponse, out var toolCall))
                {
                    // No structured tool call matched; model output represents the terminal conclusion answer
                    Program.PushDirectUiUpdate("AgentLoop", "Completed", tokensPerSecond, 2000, 32768, $"[FINAL ANSWER] {lastModelResponse}", "llm");
                    return;
                }

                // Push worker activation alert status parameters to the frontend sidebar template layout
                Program.PushDirectUiUpdate(toolCall.Function.Name, "Executing Step", tokensPerSecond, 4000, 32768, $"[DISPATCH] Activating worker target tool -> {toolCall.Function.Name}", "worker");

                var arguments = ParseArguments(toolCall.Function.Arguments);
                arguments = _normalizer.Normalize(toolCall.Function.Name, arguments);
                arguments["workspaceRoot"] = workspaceRoot;

                var toolRequest = new ToolRequest(arguments)
                {
                    ToolName = toolCall.Function.Name,
                    ExecutionContextPath = workspaceRoot
                };

                // Execute the target deterministic worker logic module through your internal routing layer
                var result = await _toolRouter.ExecuteAsync(toolCall.Function.Name, toolRequest, cancellationToken).ConfigureAwait(false);

                if (result.Success)
                {
                    Program.PushDirectUiUpdate(toolCall.Function.Name, "Success", 0.0, 5000, 32768, $"[SUCCESS] Worker run output fragment: {TruncateLogOutput(result.Output)}", "worker");
                }
                else
                {
                    Program.PushDirectUiUpdate(toolCall.Function.Name, "Execution Failure", 0.0, 5000, 32768, $"[CRASH DETECTED] Error diagnostics flag: {result.Error}", "sys");
                }

                // Synchronize continuous agent state tracing tracking logs
                history.Add(new ChatMessageContext { Role = "assistant", Content = lastModelResponse });
                history.Add(new ChatMessageContext
                {
                    Role = "tool",
                    Content = JsonSerializer.Serialize(new
                    {
                        tool = toolCall.Function.Name,
                        success = result.Success,
                        output = result.Output,
                        error = result.Error
                    })
                });

                // Circuit breaker: immediately stop the agent loop if a critical system file task crashes out
                if (!result.Success && (toolCall.Function.Name == "WriteFileWorker" || toolCall.Function.Name == "PatchFileWorker"))
                {
                    Program.PushDirectUiUpdate("AgentLoop", "Aborted", 0.0, 6000, 32768, "[CRITICAL ERROR] Core filesystem manipulation failure. Aborting sequence to secure workspace safety.", "sys");
                    return;
                }
            }

            Program.PushDirectUiUpdate("AgentLoop", "Halted", 0.0, 8000, 32768, "[SYS] Maximum compilation loop iterations reached without closing resolution target context.", "sys");
        }

        private static bool TryParseToolCall(string text, out ToolCall call)
        {
            call = new ToolCall();
            var cleaned = ExtractJsonBlock(text);
            if (string.IsNullOrWhiteSpace(cleaned)) return false;

            try
            {
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var parsed = JsonSerializer.Deserialize<ToolCall>(cleaned, options);
                if (parsed?.Function == null || string.IsNullOrWhiteSpace(parsed.Function.Name)) return false;

                call = parsed;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static string ExtractJsonBlock(string input)
        {
            var fenced = Regex.Match(input, @"```json\s*(?<json>\{.*?\})\s*```", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            if (fenced.Success) return fenced.Groups["json"].Value;

            var trimmed = input.Trim();
            return trimmed.StartsWith('{') && trimmed.EndsWith('}') ? trimmed : string.Empty;
        } 
private static Dictionary<string, object?> ParseArguments(string json){try{var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };var dict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(string.IsNullOrWhiteSpace(json) ? "{}" : json, options) ?? new Dictionary<string, JsonElement>();var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);foreach (var (key, value) in dict){result[key] = value.ValueKind switch{JsonValueKind.String => value.GetString(),JsonValueKind.Number when value.TryGetInt64(out var i) => i,JsonValueKind.Number when value.TryGetDouble(out var d) => d,JsonValueKind.True => true,JsonValueKind.False => false,JsonValueKind.Null => null,_ => value.ToString()};}return result;}catch{return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);}}private static string TruncateLogOutput(string input, int maxCharacters = 120){if (string.IsNullOrEmpty(input)) return string.Empty;return input.Length <= maxCharacters ? input : $"{input.Substring(0, maxCharacters)}... [Truncated for UI view]";}}// Secondary data structures matching tool allocation mapspublic class ToolCall{[JsonPropertyName("id")] public string Id { get; set; } = string.Empty;[JsonPropertyName("type")] public string Type { get; set; } = string.Empty;[JsonPropertyName("function")] public FunctionCall Function { get; set; } = new();}public class FunctionCall{[JsonPropertyName("name")] public string Name { get; set; } = string.Empty;[JsonPropertyName("arguments")] public string Arguments { get; set; } = string.Empty;}}

