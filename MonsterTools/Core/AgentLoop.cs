using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MonsterTools.Services;

namespace MonsterTools.Core
{
    public interface IAgentLoop
    {
        Task<ToolResult> RunAsync(string initialPrompt, Dictionary<string, object> arguments, CancellationToken cancellationToken);
    }

    public class AgentLoop : IAgentLoop
    {
        private readonly ILMStudioService _lmStudioService;
        private readonly IToolRouter _toolRouter;
        private readonly IToolArgumentNormalizer _normalizer;
        private readonly ILogger<AgentLoop> _logger;
        
        // Prevent low-compute local models from getting stuck in an expensive infinite execution loop
        private const int MaxExecutionSteps = 5;

        public AgentLoop(
            ILMStudioService lmStudioService,
            IToolRouter toolRouter,
            IToolArgumentNormalizer normalizer,
            ILogger<AgentLoop> logger)
        {
            _lmStudioService = lmStudioService ?? throw new ArgumentNullException(nameof(lmStudioService));
            _toolRouter = toolRouter ?? throw new ArgumentNullException(nameof(toolRouter));
            _normalizer = normalizer ?? throw new ArgumentNullException(nameof(normalizer));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<ToolResult> RunAsync(
            string initialPrompt, 
            Dictionary<string, object> arguments, 
            CancellationToken cancellationToken)
        {
            _logger.LogInformation("Initializing Local Agent Execution Loop for model: ibm/granite-4-h-tiny.");
            
            // Build the evolving context thread
            string conversationHistory = initialPrompt;
            int currentStep = 0;

            while (currentStep < MaxExecutionSteps)
            {
                cancellationToken.ThrowIfCancellationRequested();
                currentStep++;
                
                _logger.LogInformation("Processing Agent Loop Step {Step}/{MaxSteps}", currentStep, MaxExecutionSteps);

                // 1. Interrogate local LLM via LM Studio with current history state
                var modelResult = await _lmStudioService.ProcessPromptAsync(conversationHistory, arguments, cancellationToken);
                
                if (!modelResult.IsSuccess)
                {
                    _logger.LogError("LM Studio service execution halted at step {Step}: {Error}", currentStep, modelResult.ErrorMessage);
                    return ToolResult.Failed($"Upstream LLM runtime error: {modelResult.ErrorMessage}");
                }

                string rawModelResponse = modelResult.Payload;
                _logger.LogDebug("Raw LLM Execution Output: {Output}", rawModelResponse);

                // 2. Parse if the local model generated an explicit tool request call syntax
                var toolRequest = TryParseToolRequest(rawModelResponse);

                if (toolRequest == null)
                {
                    _logger.LogInformation("No valid deterministic tool calls requested by model. Terminal response reached.");
                    return ToolResult.Success(rawModelResponse);
                }

                // 3. Normalize arguments extracted from tool parameters to bypass hallucination inconsistencies
                _logger.LogInformation("Tool call detected: Action={Action}, Worker={Worker}", toolRequest.Action, toolRequest.TargetWorker);
                var normalizedArgs = _normalizer.Normalize(toolRequest.Arguments);

                // 4. Dispatch tasks cleanly through the internal deterministic routing layer
                _logger.LogInformation("Routing tool call execution to deterministic workers layer...");
                ToolResult executionResult = await _toolRouter.RouteAndExecuteAsync(toolRequest.TargetWorker, toolRequest.Action, normalizedArgs, cancellationToken);

                // 5. Append outcomes back into text context to update the execution state loop
                conversationHistory += $"\n[ASSISTANT TOOL_CALL]: {JsonSerializer.Serialize(toolRequest)}";
                conversationHistory += $"\n[SYSTEM TOOL_RESULT]: {JsonSerializer.Serialize(executionResult)}";
                conversationHistory += $"\n[SYSTEM DIRECTION]: Analyze the tool output data above. If the task is finished, return your definitive conclusion. Otherwise, request the next tool call phase.\n[ASSISTANT]:";
            }

            _logger.LogWarning("Agent Loop aborted. Exceeded maximum structural depth safeguards ({MaxSteps} steps).", MaxExecutionSteps);
            return ToolResult.Failed($"Execution aborted: Agent path exceeded safe limits of {MaxExecutionSteps} nested turns.");
        }

        /**
         * Safely extracts tool invocation intent strings using a resilient JSON matching pattern
         * designed for small models that struggle to yield flawless top-level JSON structures.
         */
        private ToolRequest? TryParseToolRequest(string rawText)
        {
            if (string.IsNullOrWhiteSpace(rawText)) return null;

            try
            {
                // Strict balance optimization: check if model responded with a perfect top-level wrapper
                string trimmed = rawText.Trim();
                if (trimmed.StartsWith("{") && trimmed.EndsWith("}"))
                {
                    return JsonSerializer.Deserialize<ToolRequest>(trimmed, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                }

                // Resilient fallback logic: Scan for embedded JSON snippet fragments inside conversational reasoning text
                int startIndex = trimmed.indexOf("{");
                int endIndex = trimmed.LastIndexOf("}");

                if (startIndex != -1 && endIndex > startIndex)
                {
                    string isolatedJson = trimmed.Substring(startIndex, (endIndex - startIndex) + 1);
                    var parsed = JsonSerializer.Deserialize<ToolRequest>(isolatedJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    
                    // Validate required properties to ensure it's a structural command and not a text hallucination
                    if (parsed != null && !string.IsNullOrEmpty(parsed.TargetWorker) && !string.IsNullOrEmpty(parsed.Action))
                    {
                        return parsed;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Resilient parsing bypassed. Block text does not comply with structural tool request models. Error: {Msg}", ex.Message);
            }

            return null;
        }
    }
}
