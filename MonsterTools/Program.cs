using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Http.Resilience;
using Polly;
using MonsterTools.Core;
using MonsterTools.Services;
using MonsterTools.Workers;

var builder = WebApplication.CreateBuilder(args);

// ==========================================
// 1. KESTREL & NETWORK BINDING CONFIG
// ==========================================
builder.WebHost.ConfigureKestrel(options =>
{
    // Force strict IPv4 loopback alignment to eliminate dual-stack routing drops
    options.ListenLocalhost(5105, listenOptions =>
    {
        listenOptions.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1AndHttp2;
    });
});

// ==========================================
// 2. DEPENDENCY INJECTION & SERVICES REGISTER
// ==========================================
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new() { Title = "MonsterTools Orchestration API", Version = "v1" });
});

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNameCaseInsensitive = true;
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.SerializerOptions.WriteIndented = false;
});

// Core Architectural Component Mapping
builder.Services.AddSingleton<IToolArgumentNormalizer, ToolArgumentNormalizer>();
builder.Services.AddSingleton<IToolSchemas, ToolSchemas>();
builder.Services.AddSingleton<ToolRouter>();
builder.Services.AddSingleton<AgentLoop>();

// Core Workers registered as singletons so ToolRouter can locate them
builder.Services.AddSingleton<FileSystemWorkers>();
builder.Services.AddSingleton<BuildWorker>();

// ==========================================
// 3. RESILIENT HTTP CLIENT FOR LM STUDIO
// ==========================================
builder.Services.AddHttpClient<ILMStudioService, LMStudioService>(client =>
{
    var lmStudioUri = builder.Configuration["LMStudio:BaseUrl"] ?? "http://127.0.0.1:1234";
    client.BaseAddress = new Uri(lmStudioUri);
    client.Timeout = TimeSpan.FromSeconds(120); 
})
.AddStandardResilienceHandler(options =>
{
    options.Retry.MaxRetryAttempts = 3;
    options.Retry.Delay = TimeSpan.FromSeconds(2);
    options.Retry.BackoffType = DelayBackoffType.Exponential;
    options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
    options.CircuitBreaker.FailureRatio = 0.6;
});

// ==========================================
// 4. ENTERPRISE RATE LIMITING & HEALTH 
// ==========================================
builder.Services.AddRateLimiter(options =>
{
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.User.Identity?.Name ?? context.Request.Headers.Host.ToString(),
            factory: partition => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 30,
                QueueLimit = 10,
                Window = TimeSpan.FromMinutes(1)
            }));
});

builder.Services.AddHealthChecks()
    .AddCheck("LMStudio_Endpoint_Check", () => HealthCheckResult.Healthy("Orchestrator networking is online."));

var app = builder.Build();

// Bootstrap worker instances inside the central lookup router
var router = app.Services.GetRequiredService<ToolRouter>();
router.RegisterWorker(app.Services.GetRequiredService<FileSystemWorkers>());
router.RegisterWorker(app.Services.GetRequiredService<BuildWorker>());

// ==========================================
// 5. MIDDLEWARE PIPELINE
// ==========================================
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseRateLimiter();

app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var result = JsonSerializer.Serialize(new
        {
            status = report.Status.ToString(),
            duration = report.TotalDuration,
            entries = report.Entries.Select(e => new { key = e.Key, value = e.Value.Status.ToString() })
        });
        await context.Response.WriteAsync(result);
    }
});

// ==========================================
// 6. MINIMAL API ROUTE MAPPINGS
// ==========================================
var apiGroup = app.MapGroup("/api/agent");

apiGroup.MapPost("/", async (
    [FromBody] AgentRequestContext request,
    AgentLoop agentLoop,
    IToolArgumentNormalizer normalizer,
    ILoggerFactory loggerFactory,
    CancellationToken cancellationToken) =>
{
    var logger = loggerFactory.CreateLogger("AgentOrchestrationEndpoint");

    if (request == null || string.IsNullOrWhiteSpace(request.Prompt))
    {
        return Results.BadRequest(new { error = "Request prompt payload cannot be empty." });
    }

    try
    {
        logger.LogInformation("Processing Agent Pipeline request for Model: {Model}", request.TargetModel ?? "Default");

        var normalizedArgs = normalizer.Normalize(request.Arguments);
        
        // Execute the processing loop via the unified AgentLoop orchestrator
        var executionResult = await agentLoop.RunExecutionCycleAsync(request.Prompt, normalizedArgs, cancellationToken);

        if (!executionResult.IsSuccess)
        {
            logger.LogError("Backend orchestration loop execution failed: {Message}", executionResult.ErrorMessage);
            return Results.Json(new { error = executionResult.ErrorMessage }, statusCode: 502);
        }

        return Results.Ok(new AgentResponseContext
        {
            ResponseId = Guid.NewGuid().ToString(),
            RawOutput = executionResult.Output,
            Status = "Completed",
            Timestamp = DateTime.UtcNow
        });
    }
    catch (OperationCanceledException)
    {
        logger.LogWarning("Agent pipeline processing was aborted by the client.");
        return Results.StatusCode(499);
    }
    catch (Exception ex)
    {
        logger.LogCritical(ex, "Fatal failure during /api/agent payload routing execution.");
        return Results.Problem(detail: ex.Message, statusCode: 500, title: "Internal Pipeline Agent Error");
    }
})
.WithName("PostAgentPrompt")
.WithOpenApi(operation =>
{
    operation.Summary = "Dispatches prompt requests straight to the local LM Studio instance";
    operation.Description = "Validates models like ibm/granite-4-h-tiny and standardises missing arguments dynamically.";
    return operation;
});

app.Run();

public record AgentRequestContext(string Prompt, string? TargetModel, Dictionary<string, object> Arguments);

public record AgentResponseContext
{
    public required string ResponseId { get; init; }
    public required string RawOutput { get; init; }
    public required string Status { get; init; }
    public required DateTime Timestamp { get; init; }
}
