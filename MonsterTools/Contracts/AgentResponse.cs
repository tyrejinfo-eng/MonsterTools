namespace MonsterTools.Contracts;

public sealed class AgentResponse
{
    public bool Success { get; set; }

    public string Output { get; set; } = "";

    public string Error { get; set; } = "";
}