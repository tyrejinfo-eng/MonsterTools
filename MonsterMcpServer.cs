using System;
using System.Text.Json;
using MonsterTools.Core;
using MonsterTools.Services;

namespace MonsterTools;

public class MonsterMcpServer
{
    private readonly AgentLoop _agent;

    public MonsterMcpServer(AgentLoop agent)
    {
        _agent = agent;
    }

    public string Handle(string inputJson)
    {
        try
        {
            var request = JsonSerializer.Deserialize<McpRequest>(inputJson);

            if (request == null || string.IsNullOrWhiteSpace(request.prompt))
                return JsonSerializer.Serialize(new { error = "Invalid request" });

            var result = _agent.Run(request.prompt);

            return JsonSerializer.Serialize(new
            {
                result
            });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                error = ex.Message
            });
        }
    }

    public class McpRequest
    {
        public string prompt { get; set; } = "";
    }
}