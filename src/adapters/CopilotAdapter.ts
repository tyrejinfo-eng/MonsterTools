export interface CopilotMessage {
  role: 'system' | 'user' | 'assistant';
  content: string;
}

export interface CopilotPayload {
  messages?: CopilotMessage[];
  model?: string;
  stream?: boolean;
  workspaceRoot?: string;
}

export interface MonsterAgentRequest {
  prompt: string;
  targetModel?: string;
  arguments?: Record<string, unknown>;
  workspace?: string;
}

export class CopilotAdapter {
  static extractPrompt(body: CopilotPayload): string {
    const lineBreak = '\n';
    return (body.messages ?? [])
      .map(message => message.content)
      .filter(Boolean)
      .join(lineBreak);
  }

  static toMonsterToolsRequest(body: CopilotPayload): MonsterAgentRequest {
    const prompt = CopilotAdapter.extractPrompt(body);
    const workspace = body.workspaceRoot ?? process.cwd();

    return {
      prompt,
      targetModel: body.model ?? 'ibm/granite-4-h-tiny',
      arguments: {
        workspace
      },
      workspace
    };
  }

  static shouldUseMonsterTools(prompt: string): boolean {
    return /(build|compile|error|search|find|read file|write file|patch file|workspace|git|test|diagnostic|ast)/i.test(prompt);
  }
}
