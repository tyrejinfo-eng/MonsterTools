namespace MonsterTools.Core;

public static class ToolValidator
{
    public static ToolResult Validate(string toolName, Dictionary<string, object?> args)
    {
        if (!ToolSchemas.Tools.TryGetValue(toolName, out var schema))
            return ToolResult.Fail($"Unknown tool: {toolName}");

        foreach (var required in schema.Required)
        {
            if (!args.ContainsKey(required) || args[required] == null)
            {
                return ToolResult.Fail($"Missing required argument: {required}");
            }
        }

        return ToolResult.Ok("valid");
    }
}