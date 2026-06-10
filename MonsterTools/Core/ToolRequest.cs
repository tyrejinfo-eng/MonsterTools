using System.Collections.Generic;

namespace MonsterTools.Core
{
    public class ToolRequest
    {
        public string ToolName { get; set; } = string.Empty;
        public Dictionary<string, object> Arguments { get; set; } = new();
        public string ExecutionContextPath { get; set; } = string.Empty;
    }
}
