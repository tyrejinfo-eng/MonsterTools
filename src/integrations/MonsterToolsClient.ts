import axios from 'axios';

export interface MonsterAgentRequest {
  prompt: string;
  targetModel?: string;
  arguments?: Record<string, unknown>;
  workspace?: string;
}

export interface MonsterAgentResponse {
  success: boolean;
  output: string;
  error?: string;
}

export class MonsterToolsClient {
  constructor(private readonly baseUrl = 'http://127.0.0.1:8080') {}

  async dispatchAgentTask(payload: MonsterAgentRequest): Promise<MonsterAgentResponse> {
    const response = await axios.post<MonsterAgentResponse>(`${this.baseUrl}/api/agent`, {
      prompt: payload.prompt,
      workspace: payload.workspace ?? process.cwd(),
      targetModel: payload.targetModel ?? 'ibm/granite-4-h-tiny',
      arguments: payload.arguments ?? {}
    }, {
      headers: { 'Content-Type': 'application/json' },
      timeout: 120000
    });

    return response.data;
  }
}
