using System.Threading.Tasks;

namespace MonsterTools.Core
{
    /// <summary>
    /// Enforces structural execution mechanics across all system task workers.
    /// </summary>
    public interface IToolWorker
    {
        string Name { get; }
        string Description { get; }
        Task<ToolResult> ExecuteAsync(ToolRequest request);
    }
}
