using MonsterTools.Core;
using MonsterTools.Services;

namespace MonsterTools.Workers.Workspace;

public sealed class WriteFileWorker : IToolWorker
{
    private readonly WorkspaceService _workspace =
        new();

    public string Name => "write_file";

    public ToolResult Run(ToolRequest request)
    {
        try
        {
            var path =
                request.Get<string>("path");

            var content =
                request.Get<string>("content");

            if (string.IsNullOrWhiteSpace(path))
                return ToolResult.Fail(
                    "Missing path");

            _workspace.WriteFile(
                path,
                content ?? "");

            return ToolResult.Ok(
                "File written");
        }
        catch (Exception ex)
        {
            return ToolResult.Fail(
                ex.Message);
        }
    }
}