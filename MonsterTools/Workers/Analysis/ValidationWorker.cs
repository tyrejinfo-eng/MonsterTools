using MonsterTools.Core;

namespace MonsterTools.Runner.Workers;

public class ValidationWorker : IToolWorker
{
    public string Name => "validate";

    public ToolResult Run(ToolRequest request)
    {
        try
        {
            var input = request.Get<string>("input");

            if (string.IsNullOrWhiteSpace(input))
                return ToolResult.Fail("Empty input");

            var isValid = input.Length > 3;

            return ToolResult.Ok(isValid ? "VALID" : "INVALID");
        }
        catch (Exception ex)
        {
            return ToolResult.Fail($"ValidationWorker error: {ex.Message}");
        }
    }
}