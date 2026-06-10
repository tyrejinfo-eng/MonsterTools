using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MonsterTools.Core;
using MonsterTools.Services;

var builder = WebApplication.CreateBuilder(args);

// Register endpoints metadata profiling layers
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Inject single-instance shared system tool tracking router
builder.Services.AddSingleton<ToolRouter>();

// Register the HttpClient factory instance targeting the local loopback IPv4 parameters
builder.Services.AddHttpClient<LMStudioService>(client =>
{
    client.Timeout = System.TimeSpan.FromSeconds(60);
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Enforce standard explicit IPv4 boundary mapping
app.MapPost("/api/agent", async (ToolRequest request, AgentLoop agent, ToolRouter router) =>
{
    if (request == null) return Results.BadRequest("Malformed payload structural data.");
    
    // Core engine interface execution pipeline linkage triggers here
    return Results.Ok(new { status = "Processing payload block details securely." });
})
.WithName("ExecuteAgentEndpoint")
.WithOpenApi();

app.Run();
