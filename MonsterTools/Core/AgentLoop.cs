using MonsterTools.Services;
using System.Text.Json;

namespace MonsterTools.Core;

public class AgentLoop
{
    private readonly LMStudioService _llm;
    private readonly WorkerDispatcher _dispatcher;

    public AgentLoop(LMStudioService llm, ToolExecutor executor)
    {
        _llm = llm;
        _dispatcher = new WorkerDispatcher(executor);
    }

public async Task<string> RunAsync(
    string prompt,
    string workspace)
{
    var response =
        await _llm.AskAsync(
            ToolRouter.BuildSystemPrompt(),
            prompt);

    var toolCall =
        ToolCall.Parse(response);

    var result =
        _dispatcher.Dispatch(
            toolCall.tool,
            toolCall.args);

    return result.Output;
}

    public ToolResult RunToolDirect(string tool, Dictionary<string, object?> args)
{
    return _dispatcher.Dispatch(tool, args);
}
}