using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Text.Json;
using System.Threading.Tasks;
using MonsterTools.Services;

namespace MonsterTools
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // 1. Configure Kestrel engine explicitly to use IPv4 loopback to avoid cross-layer connection dropped issues
            builder.WebHost.ConfigureKestrel(options =>
            {
                options.ListenLocalhost(5000); // Exposes http://localhost:5000
            });

            // 2. Register core services with standard Dependency Injection lifetimes
            builder.Services.AddSingleton<LMStudioService>(sp => 
                new LMStudioService("http://127.0.0.1:1234", "ibm/granite-4-h-tiny"));
                
            builder.Services.AddSingleton<ToolExecutionEngine>();

            // Enable relaxed JSON matching parameters for multi-layer Node-to-C# communications
            builder.Services.ConfigureHttpJsonOptions(options =>
            {
                options.SerializerOptions.PropertyNameCaseInsensitive = true;
                options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            });

            var app = builder.Build();

            // 3. Simple health verification route to test layer pipeline status
            app.MapGet("/api/health", () => Results.Ok(new 
            { 
                status = "online", 
                engine = "MonsterTools C# Orchestration Core",
                timestamp = DateTime.UtcNow 
            }));

            // 4. Critical missing entry mapping endpoint connecting Layer 2 (Proxy) to Layer 3 (Engine)
            app.MapPost("/api/agent", async (HttpContext context, ToolExecutionEngine executionEngine) =>
            {
                try
                {
                    // Read the incoming payload text directly to protect against malformed structural streams
                    using var reader = new System.IO.StreamReader(context.Request.Body);
                    string rawJson = await reader.ReadToEndAsync();

                    if (string.IsNullOrWhiteSpace(rawJson))
                    {
                        return Results.BadRequest(new { error = "Inbound execution payload framework cannot be blank." });
                    }

                    // Hand raw string directly down to ToolExecutionEngine to process routing loops safely
                    string engineResult = await executionEngine.ExecuteAsync(rawJson);

                    // Output results back up to proxy pipeline layer
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsync(engineResult);
                    return Results.Empty;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Engine Route Error] Exception caught in API context: {ex.Message}");
                    return Results.Json(new 
                    { 
                        success = false, 
                        output = "Catastrophic error handling tool execution request route pipeline.",
                        error = ex.Message 
                    }, statusCode: 500);
                }
            });

            Console.WriteLine("================================================================");
            Console.WriteLine(" MonsterTools C# Orchestration Core Engine Engine Starting...");
            Console.WriteLine(" Listening Endpoint Profile Active: http://localhost:5000");
            Console.WriteLine(" Ready to route inbound requests from copilot-proxy-tools layer...");
            Console.WriteLine("================================================================");

            app.Run();
        }
    }
}
