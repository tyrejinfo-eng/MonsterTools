using MonsterTools.Core;
using MonsterTools.Services;
using MonsterTools.Workers.Analysis;
using MonsterTools.Workers.Build;
using MonsterTools.Workers.Git;
using MonsterTools.Workers.Workspace;
using MonsterTools.Integrations;

namespace MonsterTools.Host;

public sealed class MonsterToolsHost
{
    private ToolExecutor? _executor;
    private AgentLoop? _agent;
    private McpServer? _mcpServer;
    private LMStudioService? _lmStudio;
    private ProxyBridge? _proxyBridge;

    public async Task StartAsync()
    {
        Console.WriteLine("[HOST] Initializing...");

        var workers = BuildWorkers();

        _executor = new ToolExecutor(workers);

        _lmStudio = new LMStudioService();

        Console.WriteLine("[HOST] Checking LM Studio...");

        var connected = await _lmStudio.HealthCheckAsync();

        Console.WriteLine(
            connected
                ? "[HOST] LM Studio Online"
                : "[HOST] LM Studio Offline");

        _agent = new AgentLoop(
            _lmStudio,
            _executor);

        _mcpServer = new McpServer(_agent);

        _proxyBridge = new ProxyBridge(
            _executor,
            _lmStudio);

        Console.WriteLine("[HOST] Startup Complete");
    }

    public async Task RunAsync()
    {
        if (_mcpServer == null)
            throw new InvalidOperationException();

        Console.WriteLine("[HOST] MCP Ready");

        await Task.Run(() =>
        {
            _mcpServer.Run();
        });
    }

    private static IToolWorker[] BuildWorkers()
    {
        return
        [
            new WorkspaceScanWorker(),

            new ReadFileWorker(),
            new WriteFileWorker(),
            new PatchFileWorker(),

            new SearchWorker(),
            new AstWorker(),
            new ValidationWorker(),

            new BuildWorker(),
            new CompilerWorker(),
            new TestWorker(),

            new GitWorker()
        ];
    }
}