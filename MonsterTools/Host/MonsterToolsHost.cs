using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Hosting;
using MonsterTools.Core;
using MonsterTools.Integrations;
using MonsterTools.Services;
using MonsterTools.Workers.Analysis;
using MonsterTools.Workers.Build;
using MonsterTools.Workers.Git;
using MonsterTools.Workers.Workspace;

namespace MonsterTools.Host;

public sealed class MonsterToolsHostService : IHostedService
{
    private Process? _proxyProcess;
    private readonly IHostEnvironment _env;

    public MonsterToolsHostService(IHostEnvironment env)
    {
        _env = env;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        Console.WriteLine("[Host Engine] Registering MonsterTools Core Engine Layer...");

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

        var agent = new AgentLoop(lmStudio, new ToolRouter(executor), normalizer);
        var bridge = new ProxyBridge(executor, lmStudio);

        _ = bridge;
        _ = dispatcher;
        _ = agent;

        // Fire up the Layer 2 proxy pipeline automatically
        StartNodeProxyGateway();

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        StopNodeProxyGateway();
        return Task.CompletedTask;
    }

    private void StartNodeProxyGateway()
    {
        try
        {
            string baseDirectory = AppContext.BaseDirectory;
            string proxyPath = Path.GetFullPath(Path.Combine(baseDirectory, "..", "..", "..", "..", "copilot-proxy-tools"));

            if (!Directory.Exists(proxyPath))
            {
                Console.WriteLine($"[Host Warning] Node.js Gateway folder path not resolved: {proxyPath}");
                return;
            }

            Console.WriteLine($"[Host Engine] Auto-starting Copilot Proxy Gateway via powershell constraint context...");

            bool isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
            var startInfo = new ProcessStartInfo
            {
                FileName = isWindows ? "powershell.exe" : "npm",
                // Explicitly uses Bypass policy to bypass execution blocks automatically
                Arguments = isWindows ? "-NoProfile -ExecutionPolicy Bypass -Command \"npm run dev\"" : "run dev",
                WorkingDirectory = proxyPath,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            _proxyProcess = new Process { StartInfo = startInfo };

            _proxyProcess.OutputDataReceived += (sender, args) => {
                if (!string.IsNullOrEmpty(args.Data)) 
                    Console.WriteLine($"[Proxy Node Log] {args.Data}");
            };
            _proxyProcess.ErrorDataReceived += (sender, args) => {
                if (!string.IsNullOrEmpty(args.Data)) {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"[Proxy Node Error] {args.Data}");
                    Console.ResetColor();
                }
            };

            _proxyProcess.Start();
            _proxyProcess.BeginOutputReadLine();
            _proxyProcess.BeginErrorReadLine();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Host Error] Background gateway failed to load: {ex.Message}");
        }
    }

    private void StopNodeProxyGateway()
    {
        if (_proxyProcess != null && !_proxyProcess.HasExited)
        {
            try
            {
                Console.WriteLine("[Host Engine] Tearing down background Node.js processes cleanly...");
                _proxyProcess.Kill(true);
                _proxyProcess.Dispose();
                _proxyProcess = null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Host Warning] Resource cleanup exception: {ex.Message}");
            }
        }
    }
}
