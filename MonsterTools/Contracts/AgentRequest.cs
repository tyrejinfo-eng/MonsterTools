namespace MonsterTools.Contracts;

public sealed class AgentRequest
{
    public string Prompt { get; set; } = "";
    public string Workspace { get; set; } = "";
}