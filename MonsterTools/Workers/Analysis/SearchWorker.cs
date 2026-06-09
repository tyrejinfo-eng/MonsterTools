using MonsterTools.Core;
using System.IO;
using System.Collections.Generic;

namespace MonsterTools.Runner.Workers;

public class SearchWorker : IToolWorker
{
    public string Name => "SearchWorker";

    public ToolResult Run(ToolRequest request)
    {
        try
        {
            // STEP 3 — CLEAN SEARCH WORKER (NO GUESSING INSIDE WORKER)
            var pattern = request.Get<string>("pattern");

            if (string.IsNullOrWhiteSpace(pattern))
                return ToolResult.Fail("Missing pattern");

            var root = request.Get<string>("workspaceRoot")
                     ?? Environment.CurrentDirectory;

            if (!Directory.Exists(root))
                return ToolResult.Fail($"Invalid workspace root: {root}");

            var results = new List<string>();

            foreach (var file in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                foreach (var line in File.ReadLines(file))
                {
                    if (line.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                    {
                        results.Add($"{file}: {line}");
                    }
                }
            }

            return ToolResult.Ok(string.Join("\n", results));
        }
        catch (Exception ex)
        {
            return ToolResult.Fail($"SearchWorker error: {ex.Message}");
        }
    }
}