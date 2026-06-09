using System;
using System.Collections.Generic;
using System.Linq;

namespace MonsterTools.Core;

public class ToolExecutor
{
    private readonly Dictionary<string, IToolWorker> _workers;

    public ToolExecutor(IEnumerable<IToolWorker> workers)
    {
        _workers = workers.ToDictionary(w => w.Name, w => w);
    }

    public ToolResult Execute(string toolName, ToolRequest request)
    {
        if (!_workers.TryGetValue(toolName, out var worker))
            return ToolResult.Fail($"Tool '{toolName}' not found");

        return worker.Run(request);
    }
}