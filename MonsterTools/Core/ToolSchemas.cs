using System.Collections.Generic;

namespace MonsterTools.Core;

public static class ToolSchemas
{
    public static readonly IReadOnlyDictionary<string, ToolDefinition> Tools =
        new Dictionary<string, ToolDefinition>
        {
            ["workspace_scan"] = new()
            {
                Description = "Scan workspace structure and discover projects",
                Optional = new[] { "path" }
            },

            ["read_file"] = new()
            {
                Description = "Read file contents",
                Required = new[] { "path" }
            },

            ["write_file"] = new()
            {
                Description = "Write file contents",
                Required = new[] { "path", "content" }
            },

            ["patch_file"] = new()
            {
                Description = "Patch text within a file",
                Required = new[] { "path", "find", "replace" }
            },

            ["search"] = new()
            {
                Description = "Search workspace files",
                Required = new[] { "pattern" },
                Optional = new[] { "workspaceRoot" }
            },

            ["ast"] = new()
            {
                Description = "Analyze source structure using Roslyn",
                Required = new[] { "path" }
            },

            ["build"] = new()
            {
                Description = "Run dotnet build",
                Required = new[] { "path" }
            },

            ["compiler"] = new()
            {
                Description = "Run verification build",
                Required = new[] { "path" }
            },

            ["test"] = new()
            {
                Description = "Run dotnet test",
                Required = new[] { "path" }
            },

            ["git"] = new()
            {
                Description = "Run git operations",
                Optional = new[] { "path", "command" }
            },

            ["validation"] = new()
            {
                Description = "Validate tool inputs",
                Optional = new[] { "input" }
            }
        };
}

public sealed class ToolDefinition
{
    public string Description { get; init; } = string.Empty;

    public string[] Required { get; init; } = [];

    public string[] Optional { get; init; } = [];
}