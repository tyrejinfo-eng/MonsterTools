import axios, { AxiosInstance } from 'axios';

// Matches what VS Code Copilot UI provides to Layer 2
export interface MonsterAgentRequest {
  prompt: string;
  workspaceRoot?: string;
}

// Matches exactly what MonsterMcpServer.cs serializes in its handle response loop
export interface MonsterAgentResponse {
  success: boolean;
  output: string;
  error?: string;
}

export class MonsterToolsClient {
  private httpClient: AxiosInstance;
  // Fixed: Linked explicitly to port 5000 based on your live dotnet run engine log
  private readonly defaultBackendUrl = 'http://127.0.0.1:5000'; 

  constructor(baseURL?: string) {
    this.httpClient = axios.create({
      baseURL: baseURL || this.defaultBackendUrl,
      timeout: 120000, // 120-second C# upstream resilience threshold
      headers: {
        'Content-Type': 'application/json',
        'Accept': 'application/json',
        'X-Client-Source': 'Copilot-Compatibility-Proxy'
      }
    });
  }

  /**
   * Dispatches a blocking prompt request to the C# orchestration engine.
   * Matches the McpEnvelope mapping expected by MonsterMcpServer.cs.
   */
  public async dispatchAgentTask(payload: MonsterAgentRequest): Promise<MonsterAgentResponse> {
    try {
      // Maps accurately to the properties expected by the private sealed class McpEnvelope
      const normalizedPayload = {
        prompt: payload.prompt,
        workspace: payload.workspaceRoot || null
      };

      // Ensure this endpoint route accurately reflects your C# minimal API endpoint handler map (e.g., /api/agent)
      const response = await this.httpClient.post<MonsterAgentResponse>(
        '/api/agent', 
        normalizedPayload
      );
      
      return response.data;
    } catch (error: any) {
      this.handleNetworkError(error, 'dispatchAgentTask');
      throw error;
    }
  }

  /**
   * Dispatches and checks health metrics down the pipeline to the Layer 3 engine.
   */
  public async checkBackendHealth(): Promise<{ status: string; upstreamOnline: boolean }> {
    try {
      const response = await this.httpClient.get<any>('/health');
      // Accounting for your service/lmStudio JSON response structure
      const isUpstreamHealthy = response.data && response.data.lmStudio === true;
      return {
        status: isUpstreamHealthy ? 'Healthy' : 'Degraded',
        upstreamOnline: isUpstreamHealthy
      };
    } catch (error) {
      return {
        status: 'Unreachable',
        upstreamOnline: false
      };
    }
  }

  private handleNetworkError(error: any, context: string): void {
    if (error.response) {
      console.error(`[MonsterToolsClient][${context}] Backend rejected payload with status ${error.response.status}:`, error.response.data);
    } else if (error.request) {
      console.error(`[MonsterToolsClient][${context}] No response received from C# engine at ${this.httpClient.defaults.baseURL}.`);
    } else {
      console.error(`[MonsterToolsClient][${context}] Pipeline setup error:`, error.message);
    }
  }
}
