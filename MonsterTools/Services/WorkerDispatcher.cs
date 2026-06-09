using System;
using System.Collections.Generic;

namespace MonsterTools.Core;

public class WorkerDispatcher
{
    private readonly ToolExecutor _executor;

    public WorkerDispatcher(ToolExecutor executor)
    {
        _executor = executor;
    }

    public ToolResult Dispatch(string toolName, Dictionary<string, object?> args)
    {
        // STEP 1: null safety
        args ??= new Dictionary<string, object?>();

        // STEP 2: normalize (auto-fill defaults)
        var safeArgs = ToolArgumentNormalizer.Normalize(toolName, args);

        // STEP 3: validate AFTER normalization
        var validation = ToolValidator.Validate(toolName, safeArgs);
        if (!validation.Success)
            return validation;

        // STEP 4: execute
        var request = new ToolRequest(safeArgs);
        return _executor.Execute(toolName, request);
    }
}