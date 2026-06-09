using MonsterTools.Core;
using MonsterTools.Services;

namespace MonsterTools.Workers.Workspace;

public sealed class PatchFileWorker : IToolWorker
{
    private readonly WorkspaceService _workspace =
        new();

    public string Name => "patch_file";

    public ToolResult Run(ToolRequest request)
    {
        try
        {
            var path =
                request.Get<string>("path");

            var find =
                request.Get<string>("find");

            var replace =
                request.Get<string>("replace");

            if (string.IsNullOrWhiteSpace(path))
                return ToolResult.Fail(
                    "Missing path");

            if (string.IsNullOrWhiteSpace(find))
                return ToolResult.Fail(
                    "Missing find");

            _workspace.PatchFile(
                path,
                find,
                replace ?? "");

            return ToolResult.Ok(
                "File patched");
        }
        catch (Exception ex)
        {
            return ToolResult.Fail(
                ex.Message);
        }
    }
}