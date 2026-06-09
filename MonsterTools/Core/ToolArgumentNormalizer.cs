using System;
using System.Collections.Generic;

namespace MonsterTools.Core;

public static class ToolArgumentNormalizer
{
    public static Dictionary<string, object?> Normalize(string toolName, Dictionary<string, object?> args)
    {
        var result = new Dictionary<string, object?>(args ?? new());

        switch (toolName)
        {
            case "SearchWorker":
                if (!result.ContainsKey("pattern") || result["pattern"] is null)
                    result["pattern"] = "";

                if (!result.ContainsKey("workspaceRoot") || result["workspaceRoot"] is null)
                    result["workspaceRoot"] = Environment.CurrentDirectory;
                break;
        }

        return result;
    }
}