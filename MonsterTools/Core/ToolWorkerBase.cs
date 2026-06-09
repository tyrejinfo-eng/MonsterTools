namespace MonsterTools.Core;

public abstract class ToolWorkerBase : IToolWorker
{
    public abstract string Name { get; }

    protected abstract ToolResult Execute(ToolRequest request);

    public ToolResult Run(ToolRequest request)
    {
        try
        {
            var result = Execute(request);

            if (result == null)
                return ToolResult.Fail("Worker returned null");

            return result;
        }
        catch (Exception ex)
        {
            return ToolResult.Fail(ex.ToString());
        }
    }
}