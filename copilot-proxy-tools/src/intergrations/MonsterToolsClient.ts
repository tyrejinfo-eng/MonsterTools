import axios, { AxiosInstance } from 'axios';
import { SSEMessage } from '../streaming/ChunkTransformer';

// Matches the incoming request shape from VS Code Copilot UI
export interface MonsterAgentRequest {
  prompt: string;
  targetModel?: string;
  arguments?: Record<string, any>;
}

// Matches the exact shape of the C# AgentResponseContext DTO
export interface MonsterAgentResponse {
  responseId: string;
  rawOutput: string;
  status: string;
  timestamp: string;
}

export class MonsterToolsClient {
  private httpClient: AxiosInstance;
  private readonly defaultBackendUrl = 'http://127.0.0.1:5105'; // Enforced strict IPv4 loopback matching Program.cs

  constructor(baseURL?: string) {
    this.httpClient = axios.create({
      baseURL: baseURL || this.defaultBackendUrl,
      timeout: 120000, // Matching the 120-second C# upstream resilience policy
      headers: {
        'Content-Type': 'application/json',
        'Accept': 'application/json',
        'X-Client-Source': 'Copilot-Compatibility-Proxy'
      }
    });
  }

  /**
   * Dispatches a blocking prompt request to the C# Minimal API orchestration engine.
   * Leveraged during deterministic tool resolution loops.
   */
  public async dispatchAgentTask(payload: MonsterAgentRequest): Promise<MonsterAgentResponse> {
    try {
      // Normalise properties to match C# camelCase JSON property naming policy
      const normalizedPayload = {
        prompt: payload.prompt,
        targetModel: payload.targetModel || 'ibm/granite-4-h-tiny', // Default local small-footprint model
        arguments: payload.arguments || {}
      };

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
   * Dispatches and checks health metrics down the pipeline to the Layer 3 orchestration engine.
   */
  public async checkBackendHealth(): Promise<{ status: string; upstreamOnline: boolean }> {
    try {
      const response = await this.httpClient.get<{ status: string }>('/health');
      return {
        status: response.data.status,
        upstreamOnline: response.data.status === 'Healthy'
      };
    } catch (error) {
      return {
        status: 'Unreachable',
        upstreamOnline: false
      };
    }
  }

  /**
   * Global interceptor to handle system communication failures gracefully.
   */
  private handleNetworkError(error: any, context: string): void {
    if (error.response) {
      // The server responded with a status code outside the 2xx range
      console.error(`[MonsterToolsClient][${context}] Backend rejected payload with status ${error.response.status}:`, error.response.data);
    } else if (error.request) {
      // The request was made but no response was received (e.g. C# engine is offline/unmapped)
      console.error(`[MonsterToolsClient][${context}] No response received from C# engine at ${this.defaultBackendUrl}. Is Program.cs running?`);
    } else {
      // Error setting up the request configuration
      console.error(`[MonsterToolsClient][${context}] Pipeline setup error:`, error.message);
    }
  }
}
