using System.Text.Json;
using MonsterTools.Api;
using MonsterTools.Contracts;
using MonsterTools.Core;
using MonsterTools.Services;
using MonsterTools.Workers.Analysis;
using MonsterTools.Workers.Build;
using MonsterTools.Workers.Git;
using MonsterTools.Workers.Workspace;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration
    .AddJsonFile("Config/proxy.json", optional: true, reloadOnChange: true)
    .AddJsonFile("Config/lmstudio.json", optional: true, reloadOnChange: true)
    .AddJsonFile("Config/workers.json", optional: true, reloadOnChange: true);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.SerializerOptions.WriteIndented = true;
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpClient("lmstudio", client =>
{
    client.Timeout = TimeSpan.FromMinutes(2);
});

builder.Services.AddSingleton<WorkspaceService>();
builder.Services.AddSingleton<BuildService>();
builder.Services.AddSingleton<ToolArgumentNormalizer>();
builder.Services.AddSingleton<ProxyStartupService>();

builder.Services.AddSingleton<ILMStudioService>(sp =>
{
    var proxyStartup = sp.GetRequiredService<ProxyStartupService>();
    var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient("lmstudio");
    return new LMStudioService(httpClient, proxyStartup.LmStudioUri.ToString(), proxyStartup.DefaultModel);
});

builder.Services.AddSingleton<IToolWorker, WorkspaceScanWorker>();
builder.Services.AddSingleton<IToolWorker, ReadFileWorker>();
builder.Services.AddSingleton<IToolWorker, WriteFileWorker>();
builder.Services.AddSingleton<IToolWorker, PatchFileWorker>();
builder.Services.AddSingleton<IToolWorker, SearchWorker>();
builder.Services.AddSingleton<IToolWorker, AstWorker>();
builder.Services.AddSingleton<IToolWorker, ValidationWorker>();
builder.Services.AddSingleton<IToolWorker, BuildWorker>();
builder.Services.AddSingleton<IToolWorker, CompilerWorker>();
builder.Services.AddSingleton<IToolWorker, TestWorker>();
builder.Services.AddSingleton<IToolWorker, GitWorker>();

builder.Services.AddSingleton<ToolExecutor>();
builder.Services.AddSingleton<ToolRouter>();
builder.Services.AddSingleton<WorkerDispatcher>();
builder.Services.AddSingleton<AgentLoop>();
builder.Services.AddSingleton<ToolExecutionEngine>();
builder.Services.AddSingleton<LMStudioBridge>();
builder.Services.AddSingleton<MonsterMcpServer>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapGet("/health", async (ILMStudioService lmStudio) =>
{
    var healthy = await lmStudio.HealthCheckAsync();
    return Results.Ok(new
    {
        service = "MonsterTools",
        lmStudio = healthy,
        timestampUtc = DateTime.UtcNow
    });
});

app.MapPost("/api/agent", async (
    AgentRequest request,
    AgentLoop agentLoop,
    CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.Prompt))
        return Results.BadRequest(new AgentResponse
        {
            Success = false,
            Error = "Prompt is required."
        });

    var result = await agentLoop.RunAsync(request.Prompt, request.Workspace, cancellationToken);
    return Results.Ok(new AgentResponse
    {
        Success = result.Success,
        Output = result.Output,
        Error = result.Error
    });
});

app.MapPost("/api/tool/execute", async (
    ToolExecuteRequest request,
    WorkerDispatcher dispatcher,
    CancellationToken cancellationToken) =>
{
    var result = await dispatcher.DispatchAsync(request.Tool, request.Args, cancellationToken);
    return Results.Ok(new ToolExecuteResponse
    {
        Success = result.Success,
        Output = result.Output,
        Error = result.Error
    });
});

app.MapPost("/api/tools/execute", async (
    ToolExecuteRequest request,
    WorkerDispatcher dispatcher,
    CancellationToken cancellationToken) =>
{
    var result = await dispatcher.DispatchAsync(request.Tool, request.Args, cancellationToken);
    return Results.Ok(new ToolExecuteResponse
    {
        Success = result.Success,
        Output = result.Output,
        Error = result.Error
    });
});

app.MapPost("/api/tool", async (
    ToolExecuteRequest request,
    WorkerDispatcher dispatcher,
    CancellationToken cancellationToken) =>
{
    var result = await dispatcher.DispatchAsync(request.Tool, request.Args, cancellationToken);
    return Results.Ok(new ToolExecuteResponse
    {
        Success = result.Success,
        Output = result.Output,
        Error = result.Error
    });
});

app.Run();
