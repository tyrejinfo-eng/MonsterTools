using MonsterTools.Core;
using MonsterTools.Services;

namespace MonsterTools.Workers.Workspace;

public sealed class WorkspaceScanWorker : IToolWorker
{
    private readonly WorkspaceService _workspace =
        new();

    public string Name => "workspace_scan";

    public ToolResult Run(ToolRequest request)
    {
        try
        {
            var files =
                _workspace.ScanWorkspace()
                    .Take(500);

            return ToolResult.Ok(
                string.Join(
                    Environment.NewLine,
                    files));
        }
        catch (Exception ex)
        {
            return ToolResult.Fail(
                ex.Message);
        }
    }
}