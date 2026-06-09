using MonsterTools.Core;
using MonsterTools.Services;

namespace MonsterTools.Integrations;

public sealed class ProxyBridge
{
    private readonly ToolExecutor _executor;
    private readonly LMStudioService _lmStudio;

    public ProxyBridge(
        ToolExecutor executor,
        LMStudioService lmStudio)
    {
        _executor = executor;
        _lmStudio = lmStudio;
    }

    public ToolResult ExecuteTool(
        string toolName,
        Dictionary<string, object?> args)
    {
        var request = new ToolRequest(args);

        return _executor.Execute(
            toolName,
            request);
    }

    public async Task<string> ExecutePromptAsync(
        string prompt)
    {
        return await _lmStudio.AskAsync(
            """
            You are MonsterTools.
            Prefer deterministic workers.
            Use the model only when reasoning is required.
            """,
            prompt,
            0.1);
    }
}