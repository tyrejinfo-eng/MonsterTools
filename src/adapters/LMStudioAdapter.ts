import axios from 'axios';
import { Readable } from 'stream';
import { CopilotAdapter, type CopilotPayload } from './CopilotAdapter.js';
import { MonsterToolsClient } from '../integrations/MonsterToolsClient.js';

export class LMStudioAdapter {
  constructor(
    private readonly monsterTools = new MonsterToolsClient('http://127.0.0.1:8080'),
    private readonly lmStudioBaseUrl = 'http://127.0.0.1:1234/v1',
    private readonly defaultModel = 'ibm/granite-4-h-tiny'
  ) {}

  async transformAndRouteRequest(payload: CopilotPayload): Promise<Readable> {
    const prompt = CopilotAdapter.extractPrompt(payload);
    const workspace = payload.workspaceRoot ?? process.cwd();

    if (CopilotAdapter.shouldUseMonsterTools(prompt)) {
      try {
        const result = await this.monsterTools.dispatchAgentTask({
          prompt,
          workspace,
          targetModel: payload.model ?? this.defaultModel,
          arguments: { workspace }
        });

        const toolContext = [
          'MonsterTools deterministic output:',
          result.output || result.error || ''
        ].join('\n');

        const augmentedPayload: CopilotPayload = {
          ...payload,
          model: payload.model ?? this.defaultModel,
          messages: [
            ...(payload.messages ?? []).filter(message => message.role !== 'system'),
            { role: 'system', content: 'You are a local coding assistant. Use deterministic tool results as ground truth.' },
            { role: 'user', content: `${prompt}\n\n${toolContext}` }
          ],
          stream: true,
          workspaceRoot: workspace
        };

        return this.forwardToLmStudio(augmentedPayload);
      } catch (error: any) {
        return this.toSseStream(`MonsterTools routing failed: ${error.message}`);
      }
    }

    return this.forwardToLmStudio(payload);
  }

  private async forwardToLmStudio(payload: CopilotPayload): Promise<Readable> {
    const response = await axios.post(
      `${this.lmStudioBaseUrl.replace(/\/$/, '')}/chat/completions`,
      {
        model: payload.model ?? this.defaultModel,
        messages: payload.messages ?? [],
        temperature: 0.0,
        stream: true
      },
      { responseType: 'stream' }
    );

    return response.data as Readable;
  }

  private toSseStream(text: string): Readable {
    const stream = new Readable({ read() {} });
    const chunk = {
      id: `chatcmpl-${Math.random().toString(36).slice(2, 10)}`,
      object: 'chat.completion.chunk',
      created: Math.floor(Date.now() / 1000),
      model: this.defaultModel,
      choices: [{ index: 0, delta: { content: text }, finish_reason: 'stop' }]
    };

    stream.push(`data: ${JSON.stringify(chunk)}\n\n`);
    stream.push('data: [DONE]\n\n');
    stream.push(null);
    return stream;
  }
}
