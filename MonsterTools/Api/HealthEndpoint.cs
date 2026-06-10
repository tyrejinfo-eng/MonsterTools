using MonsterTools.Services;

namespace MonsterTools.Api;

public static class HealthEndpoint
{
    public static void MapHealth(
        this WebApplication app)
    {
        app.MapGet(
            "/health",
            async (
                LMStudioService lmStudio) =>
            {
                return Results.Ok(
                    new
                    {
                        service = "MonsterTools",
                        lmstudio =
                            await lmStudio
                                .HealthCheckAsync(),
                        timestamp =
                            DateTime.UtcNow
                    });
            });
    }
}