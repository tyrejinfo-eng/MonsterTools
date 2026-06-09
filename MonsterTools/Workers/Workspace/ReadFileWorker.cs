using MonsterTools.Core;
using MonsterTools.Services;

namespace MonsterTools.Workers.Workspace;

public sealed class ReadFileWorker : IToolWorker
{
    private readonly WorkspaceService _workspace =
        new();

    public string Name => "read_file";

    public ToolResult Run(ToolRequest request)
    {
        try
        {
            var path =
                request.Get<string>("path");

            if (string.IsNullOrWhiteSpace(path))
                return ToolResult.Fail(
                    "Missing path");

            var content =
                _workspace.ReadFile(path);

            return ToolResult.Ok(content);
        }
        catch (Exception ex)
        {
            return ToolResult.Fail(
                ex.Message);
        }
    }
}