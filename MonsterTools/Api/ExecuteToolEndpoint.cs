using MonsterTools.Core;
using MonsterTools.Contracts;

namespace MonsterTools.Api;

public static class ExecuteToolEndpoint
{
    public static void MapExecuteTool(
        this WebApplication app)
    {
        app.MapPost(
            "/api/tool",
            (
                ToolExecutionRequest request,
                WorkerDispatcher dispatcher) =>
            {
                var result =
                    dispatcher.Dispatch(
                        request.Tool,
                        request.Args);

                return Results.Ok(
                    new ToolExecutionResponse
                    {
                        Success = result.Success,
                        Output = result.Output,
                        Error = result.Error
                    });
            });
    }
}