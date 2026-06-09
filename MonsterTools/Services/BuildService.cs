using System.Diagnostics;

namespace MonsterTools.Services;

public sealed class BuildService
{
    public string Execute(
        string executable,
        string arguments)
    {
        var psi = new ProcessStartInfo
        {
            FileName = executable,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process =
            Process.Start(psi);

        if (process == null)
            throw new InvalidOperationException(
                $"Failed to start {executable}");

        var output =
            process.StandardOutput.ReadToEnd();

        var error =
            process.StandardError.ReadToEnd();

        process.WaitForExit();

        return output + Environment.NewLine + error;
    }

    public string Build(string path)
    {
        return Execute(
            "dotnet",
            $"build \"{path}\"");
    }

    public string Test(string path)
    {
        return Execute(
            "dotnet",
            $"test \"{path}\"");
    }
}