using MonsterTools.Core;
using System.Diagnostics;

namespace MonsterTools.Runner.Workers;

public class BuildWorker : IToolWorker
{
    public string Name => "build";

    public ToolResult Run(ToolRequest request)
    {
        try
        {
            var projectPath = request.Get<string>("path");

            if (string.IsNullOrWhiteSpace(projectPath))
                return ToolResult.Fail("Missing or invalid path");

            var psi = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"build \"{projectPath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);

            if (process == null)
                return ToolResult.Fail("Failed to start build process");

            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();

            process.WaitForExit();

            return ToolResult.Ok(output + "\n" + error);
        }
        catch (Exception ex)
        {
            return ToolResult.Fail($"BuildWorker error: {ex.Message}");
        }
    }
}