using System.Text.Json;

namespace MonsterTools.Core;

public sealed class ToolCall
{
    public string tool { get; set; } = "";

    public Dictionary<string, object?> args
        { get; set; } = new();

    public static ToolCall Parse(
        string response)
    {
        try
        {
            return JsonSerializer
                .Deserialize<ToolCall>(
                    response)
                ?? new ToolCall();
        }
        catch
        {
            return new ToolCall();
        }
    }
}