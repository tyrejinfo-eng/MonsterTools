namespace MonsterTools.Contracts;

public sealed class ToolExecuteRequest
{
    public string Tool { get; set; } = "";

    public Dictionary<string, object?> Args { get; set; }
        = new();
}