using System.Text;

namespace MonsterTools.Services;

public sealed class WorkspaceService
{
    public string Resolve(
        string? workspace)
    {
        if (!string.IsNullOrWhiteSpace(
            workspace))
        {
            return workspace;
        }

        return Directory
            .GetCurrentDirectory();
    }
}

public sealed class WorkspaceService
{
    public string WorkspaceRoot { get; }

    public WorkspaceService(string? workspaceRoot = null)
    {
        WorkspaceRoot =
            workspaceRoot ??
            Directory.GetCurrentDirectory();
    }

    public bool FileExists(string path)
    {
        return File.Exists(GetSafePath(path));
    }

    public string ReadFile(string path)
    {
        var fullPath = GetSafePath(path);

        if (!File.Exists(fullPath))
            throw new FileNotFoundException(fullPath);

        return File.ReadAllText(
            fullPath,
            Encoding.UTF8);
    }

    public void WriteFile(
        string path,
        string content)
    {
        var fullPath = GetSafePath(path);

        var dir = Path.GetDirectoryName(fullPath);

        if (!string.IsNullOrWhiteSpace(dir))
            Directory.CreateDirectory(dir);

        File.WriteAllText(
            fullPath,
            content,
            Encoding.UTF8);
    }

    public void PatchFile(
        string path,
        string find,
        string replace)
    {
        var content = ReadFile(path);

        content = content.Replace(
            find,
            replace,
            StringComparison.Ordinal);

        WriteFile(path, content);
    }

    public IEnumerable<string> ScanWorkspace(
        string pattern = "*.*")
    {
        return Directory.GetFiles(
            WorkspaceRoot,
            pattern,
            SearchOption.AllDirectories);
    }

    private string GetSafePath(string path)
    {
        var fullPath =
            Path.GetFullPath(path);

        return fullPath;
    }
}