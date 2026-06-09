using MonsterTools.Api;
using MonsterTools.Core;
using MonsterTools.Services;
using MonsterTools.Workers;

var builder =
    WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<
    LMStudioService>();

builder.Services.AddSingleton<
    ReadFileWorker>();

builder.Services.AddSingleton<
    WriteFileWorker>();

builder.Services.AddSingleton<
    PatchFileWorker>();

builder.Services.AddSingleton<
    SearchWorker>();

builder.Services.AddSingleton<
    BuildWorker>();

builder.Services.AddSingleton<
    WorkspaceWorker>();

builder.Services.AddSingleton<
    ValidationWorker>();

builder.Services.AddSingleton<
    IEnumerable<IToolWorker>>(sp =>
    [
        sp.GetRequiredService<ReadFileWorker>(),
        sp.GetRequiredService<WriteFileWorker>(),
        sp.GetRequiredService<PatchFileWorker>(),
        sp.GetRequiredService<SearchWorker>(),
        sp.GetRequiredService<BuildWorker>(),
        sp.GetRequiredService<WorkspaceWorker>(),
        sp.GetRequiredService<ValidationWorker>()
    ]);

builder.Services.AddSingleton<
    ToolExecutor>();

builder.Services.AddSingleton<
    WorkerDispatcher>();

builder.Services.AddSingleton<
    AgentLoop>();

var app = builder.Build();

app.MapHealth();
app.MapExecuteTool();
app.MapExecuteAgent();

app.Run(
    "http://0.0.0.0:8080");