using System;
using System.IO;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MonsterTools.Core;
using MonsterTools.Services;
using MonsterTools.Workers;
using MonsterTools.Workers.Analysis;
using MonsterTools.Workers.Build;
using MonsterTools.Workers.Git;
using MonsterTools.Workers.Workspace;

namespace MonsterTools
{
    public class Program
    {
        private static HttpListener _httpListener;
        private static WebSocket _uiWebSocket;
        private static IServiceProvider _serviceProvider;
        private static WorkspaceListener _workspaceListener;

        public static async Task Main(string[] args)
        {
            // 1. Process explicit UI execution path arguments passed from monster-ui
            string workspacePath = GetArgumentValue(args, "--workspace") ?? Directory.GetCurrentDirectory();
            Console.WriteLine($"[CORE] Initializing MonsterTools Engine Daemon on: {workspacePath}");

            // 2. Build explicit JSON infrastructure configurations
            var configuration = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("Config/lmstudio.json", optional: true, reloadOnChange: true)
                .AddJsonFile("Config/workers.json", optional: true, reloadOnChange: true)
                .Build();

            // 3. Register standard high-performance engine service architectures
            var services = new ServiceCollection();
            ConfigureEngineServices(services, configuration);
            _serviceProvider = services.BuildServiceProvider();

            // 4. Spin up the presentation layer telemetry WebSocket socket server loopback
            StartUiControlBridge(8095);

            // 5. Fire up the local recursive file-system tracking observer
            _workspaceListener = new WorkspaceListener(workspacePath);
            _workspaceListener.StartListening();

            // 6. Bootstrap the core multi-file AgentLoop orchestrator tracking state
            var agentLoop = _serviceProvider.GetRequiredService<AgentLoop>();
            _ = Task.Run(() => agentLoop.StartOrchestratorLoop(workspacePath));

            // Keep the background daemon alive until killed cleanly by the IDE runtime
            var keepAliveToken = new CancellationTokenSource();
            await Task.Delay(Timeout.Infinite, keepAliveToken.Token).ConfigureAwait(false);
        }

        private static void ConfigureEngineServices(IServiceCollection services, IConfiguration configuration)
        {
            services.AddLogging(configure => configure.AddConsole());
            services.AddSingleton(configuration);

            services.AddHttpClient("lmstudio", client =>
            {
                client.Timeout = TimeSpan.FromMinutes(3); // Expanded allowance for low-compute execution
            });

            // Core Architectural Framework State Layers
            services.AddSingleton<WorkspaceService>();
            services.AddSingleton<BuildService>();
            services.AddSingleton<ToolArgumentNormalizer>();
            services.AddSingleton<ToolExecutor>();
            services.AddSingleton<ToolRouter>();
            services.AddSingleton<WorkerDispatcher>();
            services.AddSingleton<AgentLoop>();
            services.AddSingleton<ToolExecutionEngine>();
            services.AddSingleton<LMStudioBridge>();
            services.AddSingleton<MonsterMcpServer>();

            // Clean direct injection provider targeting LM Studio endpoints
            services.AddSingleton<ILMStudioService>(sp =>
            {
                var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient("lmstudio");
                string baseUri = configuration["LMStudio:Uri"] ?? "http://localhost:1234";
                string targetModel = configuration["LMStudio:DefaultModel"] ?? "ibm/granite-4-h-tiny";
                return new LMStudioService(httpClient, baseUri, targetModel);
            });

            // Clean functional injection mapping for individual deterministic tools
            services.AddSingleton<IToolWorker, WorkspaceScanWorker>();
            services.AddSingleton<IToolWorker, ReadFileWorker>();
            services.AddSingleton<IToolWorker, WriteFileWorker>();
            services.AddSingleton<IToolWorker, PatchFileWorker>();
            services.AddSingleton<IToolWorker, SearchWorker>();
            services.AddSingleton<IToolWorker, AstWorker>();
            services.AddSingleton<IToolWorker, ValidationWorker>();
            services.AddSingleton<IToolWorker, BuildWorker>();
            services.AddSingleton<IToolWorker, CompilerWorker>();
            services.AddSingleton<IToolWorker, TestWorker>();
            services.AddSingleton<IToolWorker, GitWorker>();
        }

        private static void StartUiControlBridge(int port)
        {
            _httpListener = new HttpListener();
            _httpListener.Prefixes.Add($"http://localhost:{port}/");
            _httpListener.Start();

            Task.Run(async () =>
            {
                while (_httpListener.IsListening)
                {
                    try
                    {
                        var context = await _httpListener.GetContextAsync().ConfigureAwait(false);
                        if (context.Request.IsWebSocketRequest)
                        {
                            var wsContext = await context.AcceptWebSocketAsync(subProtocol: null).ConfigureAwait(false);
                            _uiWebSocket = wsContext.WebSocket;
                            _ = Task.Run(() => ListenForUiPrompts(_uiWebSocket));
                            PushDirectUiUpdate("System", "Ready", 0.0, 0, 0, "[CORE] Connected to Extension Sidebar Interface Panel.", "sys");
                        }
                        else
                        {
                            context.Response.StatusCode = 400;
                            context.Response.Close();
                        }
                    }
                    catch { /* Handle cleanup or drops cleanly */ }
                }
            });
        }

        private static async Task ListenForUiPrompts(WebSocket socket)
        {
            var buffer = new byte[1024 * 8];
            while (socket.State == WebSocketState.Open)
            {
                var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None).ConfigureAwait(false);
                if (result.MessageType == WebSocketMessageType.Text)
                {
                    string rawJson = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    try
                    {
                        using var doc = JsonDocument.Parse(rawJson);
                        var root = doc.RootElement;
                        
                        if (root.TryGetProperty("event", out var eventType) && eventType.GetString() == "userPrompt")
                        {
                            string promptText = root.GetProperty("text").GetString();
                            // Route the incoming user text prompt payload directly into the waiting AgentLoop engine instance
                            var agent = _serviceProvider.GetRequiredService<AgentLoop>();
                            _ = Task.Run(() => agent.InjectUserInstruction(promptText));
                        }
                        else if (root.TryGetProperty("event", out var sysEvent) && sysEvent.GetString() == "systemRollback")
                        {
                            var gitWorker = _serviceProvider.GetRequiredService<IToolWorker>() as GitWorker;
                            gitWorker?.ExecuteRollbackAction();
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[RPC ERROR] Malformed request format intercepted: {ex.Message}");
                    }
                }
                else if (result.MessageType == WebSocketMessageType.Close)
                {
                    await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None).ConfigureAwait(false);
                }
            }
        }

        public static void PushDirectUiUpdate(string worker, string action, double tps, int tokensUsed, int maxTokens, string streamChunk, string logType = "worker")
        {
            if (_uiWebSocket == null || _uiWebSocket.State != WebSocketState.Open) return;

            var packet = new
            {
                ActiveWorker = worker,
                CurrentAction = action,
                TokensPerSecond = tps,
                ContextUsed = tokensUsed,
                ContextMax = maxTokens,
                RawStreamChunk = streamChunk,
                LogType = logType
            };

            string payloadString = JsonSerializer.Serialize(packet, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            byte[] outputBuffer = Encoding.UTF8.GetBytes(payloadString);

            Task.Run(async () =>
            {
                try
                {
                    await _uiWebSocket.SendAsync(new ArraySegment<byte>(outputBuffer), WebSocketMessageType.Text, endOfMessage: true, CancellationToken.None).ConfigureAwait(false);
                }
                catch { /* Suppress structural socket drop writing content */ }
            });
        }

        private static string GetArgumentValue(string[] args, string flag)
        {
            for (int i = 0; i < args.Length; i++)
            {if (args[i].StartsWith(flag + "=")) return args[i].Split('=')[1];if (args[i] == flag && i + 1 < args.Length) return args[i + 1];}return null;}}}
