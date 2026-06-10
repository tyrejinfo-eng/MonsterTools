using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using MonsterTools.Services;

namespace MonsterTools.Core
{
    /// <summary>
    /// The core multi-turn orchestration engine that coordinates tools and processes 
    /// local model completions.
    /// </summary>
    public class AgentLoop
    {
        private readonly ToolRouter _toolRouter;
        private readonly ILMStudioService _lmStudioService;
        private readonly ILogger<AgentLoop> _logger;
        private const int MaxExecutionTurns = 5;

        public AgentLoop(ToolRouter toolRouter, ILMStudioService lmStudioService, ILogger<AgentLoop> logger)
        {
            _toolRouter = toolRouter ?? throw new ArgumentNullException(nameof(toolRouter));
            _lmStudioService = lmStudioService ?? throw new ArgumentNullException(nameof(lmStudioService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Runs an event-driven tool execution loop, feeding results back to the model 
        /// across multiple turns until the task is complete.
        /// </summary>
        public async Task<AgentExecutionResult> RunExecutionCycleAsync(
            string userPrompt, 
            Dictionary<string, object> initialArguments, 
            CancellationToken cancellationToken)
        {
            _logger.LogInformation("Initializing agent loop workflow context for prompt.");
            
            // Build the primary, ongoing execution history array for the session
            var sessionContextHistory = new List<ChatMessageContext>
            {
                new() { Role = "system", Content = "You are an advanced C# agent loop. You have access to local deterministic tools. If you need a tool, respond with a valid JSON tool call matching the schema. If you have completed the objective, respond with your final direct answer." }
            };

            // Inject initial workspace arguments directly into the user query context
            string formattedArgumentsJson = JsonSerializer.Serialize(initialArguments);
            sessionContextHistory.Add(new ChatMessageContext 
            { 
                Role = "user", 
                Content = $"Context Arguments: {formattedArgumentsJson}\nTask: {userPrompt}" 
            });

            int currentTurnBudget = 0;
            string lastRawModelOutput = string.Empty;

            while (currentTurnBudget < MaxExecutionTurns)
            {
                cancellationToken.ThrowIfCancellationRequested();
                currentTurnBudget++;
                _logger.LogInformation("Executing iteration cycle turn {Turn}/{MaxTurns}", currentTurnBudget, MaxExecutionTurns);

                // Send the updated conversation history down to the local inference engine
                string responsePayload = await _lmStudioService.QueryModelHistoryAsync(sessionContextHistory, cancellationToken);
                lastRawModelOutput = responsePayload;

                // Stash the model's raw text generation choice in the history context
                sessionContextHistory.Add(new ChatMessageContext { Role = "assistant", Content = responsePayload });

                // Check if the model is attempting to trigger a structural tool invocation
                if (!TryExtractToolCall(responsePayload, out ToolCall? detectedCall) || detectedCall == null)
                {
                    _logger.LogInformation("No explicit tool invocation syntax caught. Terminating agent loop with final answer.");
                    return AgentExecutionResult.Success(responsePayload);
                }

                _logger.LogWarning("Detected tool invocation request for tool: '{Tool}'", detectedCall.Function.Name);

                if (!_toolRouter.HasWorker(detectedCall.Function.Name))
                {
                    string missingToolError = $"Error: The local system tool '{detectedCall.Function.Name}' is not registered.";
                    _logger.LogError("{Error}", missingToolError);
                    sessionContextHistory.Add(new ChatMessageContext { Role = "user", Content = missingToolError });
                    continue;
                }

                try
                {
                    // Locate the worker within the singleton router container matrix
                    IToolWorker targetedWorker = _toolRouter.GetWorker(detectedCall.Function.Name);
                    
                    // Parse the tool parameters safely out of the model's response string
                    var argumentMap = JsonSerializer.Deserialize<Dictionary<string, object>>(
                        string.IsNullOrWhiteSpace(detectedCall.Function.Arguments) ? "{}" : detectedCall.Function.Arguments
                    ) ?? new Dictionary<string, object>();

                    var toolRequest = new ToolRequest
                    {
                        ToolName = detectedCall.Function.Name,
                        Arguments = argumentMap
                    };

                    _logger.LogInformation("Handing off execution safely to system worker: {WorkerName}", targetedWorker.Name);
                    ToolResult executionOutput = await targetedWorker.ExecuteAsync(toolRequest);

                    // Format and append the execution result so the model can read it next turn
                    string standardizedResultPayload = JsonSerializer.Serialize(new {
                        tool = detectedCall.Function.Name,
                        success = executionOutput.IsSuccess,
                        output = executionOutput.Output,
                        error = executionOutput.ErrorMessage
                    });

                    sessionContextHistory.Add(new ChatMessageContext { Role = "user", Content = $"Tool Execution Result: {standardizedResultPayload}" });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Fatal crash intercepted during execution of worker logic.");
                    sessionContextHistory.Add(new ChatMessageContext { Role = "user", Content = $"Tool Execution Crash Failure: {ex.Message}" });
                }
            }

            _logger.LogCritical("Agent execution turn limits exceeded safety bounds.");
            return AgentExecutionResult.Failure($"The orchestration execution loop timed out or exceeded its maximum turnaround budget of {MaxExecutionTurns} turns.");
        }

        /// <summary>
        /// Cleans raw text blocks and extracts valid JSON tool call schemas.
        /// </summary>
        private static bool TryExtractToolCall(string input, out ToolCall? toolCall)
        {
            toolCall = null;
            string cleanSegment = input.Trim();

            // Strip out markdown wraps code blocks frequently emitted by low-compute LLMs
            if (cleanSegment.Contains("```json"))
            {
                int startIndex = cleanSegment.IndexOf("```json") + 7;
                int endIndex = cleanSegment.LastIndexOf("```");
                if (endIndex > startIndex)
                {
                    cleanSegment = cleanSegment[startIndex..endIndex].Trim();
                }
            }
            else if (cleanSegment.Contains("```"))
            {
                int startIndex = cleanSegment.IndexOf("```") + 3;
                int endIndex = cleanSegment.LastIndexOf("```");
                if (endIndex > startIndex)
                {
                    cleanSegment = cleanSegment[startIndex..endIndex].Trim();
                }
            }

            if (!cleanSegment.StartsWith("{") || !cleanSegment.EndsWith("}"))
            {
                return false;
            }

            try
            {
                var partialParse = JsonSerializer.Deserialize<ToolCall>(cleanSegment);
                if (partialParse != null && !string.IsNullOrWhiteSpace(partialParse.Function.Name))
                {
                    toolCall = partialParse;
                    return true;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }
    }

    public class AgentExecutionResult
    {
        public bool IsSuccess { get; set; }
        public string Output { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;

        public static AgentExecutionResult Success(string output) => new() { IsSuccess = true, Output = output };
        public static AgentExecutionResult Failure(string error) => new() { IsSuccess = false, ErrorMessage = error };
    }
}
