namespace MonsterTools.Core;

public class McpRequest
{
    public string id { get; set; } = "";
    public string type { get; set; } = "";
    public string tool { get; set; } = "";
    public Dictionary<string, object?> args { get; set; } = new();
}

public class McpResponse
{
    public string id { get; set; } = "";
    public string type { get; set; } = "tool.result";
    public bool success { get; set; }
    public object? result { get; set; }
}