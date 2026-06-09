using MonsterTools.Core;
using MonsterTools.Contracts;

namespace MonsterTools.Api;

public static class ExecuteAgentEndpoint
{
    public static void MapExecuteAgent(
        this WebApplication app)
    {
        app.MapPost(
            "/api/agent",
            async (
                AgentRequest request,
                AgentLoop agent) =>
            {
                if (string.IsNullOrWhiteSpace(
                    request.Prompt))
                {
                    return Results.BadRequest(
                        "Prompt required");
                }

                var result =
                    await agent.RunAsync(
                        request.Prompt,
                        request.Workspace);

                return Results.Ok(
                    new AgentResponse
                    {
                        Success = true,
                        Result = result
                    });
            });
    }
}