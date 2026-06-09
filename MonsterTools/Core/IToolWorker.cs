namespace MonsterTools.Core;

public interface IToolWorker
{
    string Name { get; }
    ToolResult Run(ToolRequest request);
}