using System;
using System.IO;
using System.Text;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using MonsterTools.Core;

namespace MonsterTools.Workers
{
    /// <summary>
    /// Encapsulates isolated, non-blocking .NET build execution workflows 
    /// for local agent loop feedback.
    /// </summary>
    public class BuildWorker : IToolWorker
    {
        private readonly ILogger<BuildWorker> _logger;

        public string Name => "BuildTool";
        public string Description => "Runs deterministic compilation checks on local .NET projects via the dotnet CLI.";

        public BuildWorker(ILogger<BuildWorker> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Asynchronously executes project build commands and surfaces structured compiler telemetry.
        /// </summary>
        public async Task<ToolResult> ExecuteAsync(ToolRequest request)
        {
            if (request == null)
                return ToolResult.Failure("Execution parameters cannot be null.");

            // Extract and sanitize target project workspace directory paths
            if (!request.Arguments.TryGetValue("projectPath", out var rawPath) || rawPath == null)
            {
                return ToolResult.Failure("Missing required argument parameter: 'projectPath'.");
            }

            string targetPath = rawPath.ToString()?.Trim('\"', '\'') ?? string.Empty;

            if (string.IsNullOrWhiteSpace(targetPath))
            {
                return ToolResult.Failure("Target project path variable cannot be blank.");
            }

            try
            {
                // Resolve and validate structural filesystem targets safely
                string cleanFullPath = Path.GetFullPath(targetPath);
                
                if (!Directory.Exists(cleanFullPath) && !File.Exists(cleanFullPath))
                {
                    return ToolResult.Failure($"Target path structure does not exist on local filesystem: {cleanFullPath}");
                }

                _logger.LogInformation("Spawning asynchronous compiler runtime task target: {Path}", cleanFullPath);

                // Initialize secure process boundaries - avoiding direct shell execution strings
                var compilerProcessInfo = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    WorkingDirectory = Directory.Exists(cleanFullPath) ? cleanFullPath : Path.GetDirectoryName(cleanFullPath)
                };

                // Inject secure argument lists directly to eliminate token parsing vectors
                compilerProcessInfo.ArgumentList.Add("build");
                compilerProcessInfo.ArgumentList.Add("--configuration");
                compilerProcessInfo.ArgumentList.Add("Debug");

                using var buildProcess = new Process { StartInfo = compilerProcessInfo };

                var outputAccumulator = new StringBuilder();
                var errorAccumulator = new StringBuilder();

                // Wire up asynchronous output streaming to prevent process deadlocks
                buildProcess.OutputDataReceived += (sender, args) => { if (args.Data != null) outputAccumulator.AppendLine(args.Data); };
                buildProcess.ErrorDataReceived += (sender, args) => { if (args.Data != null) errorAccumulator.AppendLine(args.Data); };

                if (!buildProcess.Start())
                {
                    return ToolResult.Failure("Failed to initialize the local 'dotnet' system subsystem infrastructure.");
                }

                // Begin reading the execution streams asynchronously
                buildProcess.BeginOutputReadLine();
                buildProcess.BeginErrorReadLine();

                // Enforce a hard 60-second compilation timeout to prevent stalled loops
                using var timeoutSource = new CancellationTokenSource(TimeSpan.FromSeconds(60));
                
                try
                {
                    await buildProcess.WaitForExitAsync(timeoutSource.Token);
                }
                catch (OperationCanceledException)
                {
                    buildProcess.Kill(true);
                    return ToolResult.Failure("Compilation worker processing dropped: Operation exceeded execution time allocation constraints (60s).");
                }

                string standardOutput = outputAccumulator.ToString();
                string errorOutput = errorAccumulator.ToString();

                bool compileSucceeded = buildProcess.ExitCode == 0;

                if (compileSucceeded)
                {
                    _logger.LogInformation("Worker 'BuildTool' compilation verified successfully.");
                    return ToolResult.Success($"Compilation Successful.\n{ExtractSummary(standardOutput)}");
                }

                _logger.LogWarning("Project build failed. Surfacing telemetry feedback details to agent loop context.");
                return new ToolResult
                {
                    IsSuccess = false,
                    Output = standardOutput,
                    ErrorMessage = $"Build Error (Code {buildProcess.ExitCode}):\n{errorOutput}\nSummary details:\n{ExtractCompilerErrors(standardOutput)}"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Critical crash encountered during project compilation check handling.");
                return ToolResult.Failure($"Build subsystem structural failure: {ex.Message}");
            }
        }

        /// <summary>
        /// Filters system output logs to extract diagnostic build errors.
        /// </summary>
        private static string ExtractCompilerErrors(string fullLogOutput)
        {
            if (string.IsNullOrWhiteSpace(fullLogOutput)) return "No output stream logs recorded.";
            
            var lines = fullLogOutput.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            var filteredDiagnostics = new StringBuilder();

            foreach (var line in lines)
            {
                // Isolate common build patterns (like 'error CS')
                if (line.Contains("error CS") || line.Contains("Build FAILED"))
                {
                    filteredDiagnostics.AppendLine(line.Trim());
                }
            }

            return filteredDiagnostics.Length > 0 ? filteredDiagnostics.ToString() : "Unable to isolate explicit compiler error messages.";
        }

        /// <summary>
        /// Captures the trailing output summary from the build logs.
        /// </summary>
        private static string ExtractSummary(string fullLogOutput)
        {
            if (string.IsNullOrWhiteSpace(fullLogOutput)) return string.Empty;
            var lines = fullLogOutput.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length <= 3) return fullLogOutput;
            
            return string.Join(Environment.NewLine, lines[^Math.Min(3, lines.Length)..]);
        }
    }
}
