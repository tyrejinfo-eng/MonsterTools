using System;
using System.Text.Json;
using System.Threading.Tasks;
using MonsterTools.Workers;

namespace MonsterTools.Services
{
    public class ToolExecutionEngine
    {
        private readonly LMStudioService _lmStudio;

        public ToolExecutionEngine(LMStudioService lmStudio)
        {
            _lmStudio = lmStudio;
        }

        public async Task<string> ExecuteAsync(string jsonPayload)
        {
            try
            {
                using var doc = JsonDocument.Parse(jsonPayload);
                var root = doc.RootElement;

                if (!root.TryGetProperty("toolName", out var toolNameProp))
                {
                    return JsonSerializer.Serialize(new { success = false, output = "Missing toolName property." });
                }

                string toolName = toolNameProp.GetString() ?? "";
                
                // Route actions cleanly down to structural worker components
                if (toolName.Equals("build_project", StringComparison.OrdinalIgnoreCase))
                {
                    var worker = new BuildWorker();
                    return await worker.ExecuteTaskAsync(root);
                }
                
                if (toolName.Equals("search_files", StringComparison.OrdinalIgnoreCase))
                {
                    var worker = new SearchWorkers();
                    return await worker.ExecuteTaskAsync(root);
                }

                return JsonSerializer.Serialize(new { success = false, output = $"Unsupported tool: {toolName}" });
            }
            catch (JsonException ex)
            {
                return JsonSerializer.Serialize(new { success = false, error = $"JsonException: {ex.Message}" });
            }
            catch (Exception ex)
            {
                return JsonSerializer.Serialize(new { success = false, error = ex.Message });
            }
        }
    }
}
