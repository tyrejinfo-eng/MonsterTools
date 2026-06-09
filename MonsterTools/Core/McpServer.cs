using System.Text.Json;

namespace MonsterTools.Core;

public class McpServer
{
    private readonly AgentLoop _agent;

    public McpServer(AgentLoop agent)
    {
        _agent = agent;
    }

    public void Run()
    {
        while (true)
        {
            var line = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(line))
                continue;

            McpRequest? request;

            try
            {
                request = JsonSerializer.Deserialize<McpRequest>(line);
            }
            catch
            {
                continue;
            }

            if (request == null)
                continue;

            var result = _agent.RunToolDirect(request.tool, request.args);

            var response = new McpResponse
            {
                id = request.id,
                success = result.Success,
                result = result.Output ?? result.Error
            };

            Console.WriteLine(JsonSerializer.Serialize(response));
        }
    }
}