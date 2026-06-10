using MonsterTools.Core;
using MonsterTools.Integrations;
using MonsterTools.Services;
using MonsterTools.Workers.Analysis;
using MonsterTools.Workers.Build;
using MonsterTools.Workers.Git;
using MonsterTools.Workers.Workspace;

namespace MonsterTools.Host;

public sealed class MonsterToolsHost
{
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        var buildService = new BuildService();
        var workspace = new WorkspaceService();
        var workers = new IToolWorker[]
        {
            new WorkspaceScanWorker(workspace),
            new ReadFileWorker(workspace),
            new WriteFileWorker(workspace),
            new PatchFileWorker(workspace),
            new SearchWorker(workspace),
            new AstWorker(workspace),
            new ValidationWorker(),
            new BuildWorker(workspace),
            new CompilerWorker(buildService, workspace),
            new TestWorker(buildService, workspace),
            new GitWorker(workspace),
            new FileSystemWorkers(workspace)
        };

        var executor = new ToolExecutor(workers);
        var dispatcher = new WorkerDispatcher(executor, new ToolArgumentNormalizer(), workspace);
        var httpClient = new HttpClient();
        var lmStudio = new LMStudioService(httpClient, "http://127.0.0.1:1234", "ibm/granite-4-h-tiny");
        var normalizer = new ToolArgumentNormalizer();

        var agent = new AgentLoop(
            lmStudio,
            new ToolRouter(executor),
            normalizer);
        var bridge = new ProxyBridge(executor, lmStudio);

        _ = bridge;
        _ = dispatcher;
        _ = agent;

        await Task.CompletedTask;
    }
}
