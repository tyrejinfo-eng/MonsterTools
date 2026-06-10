import { MonsterAgentRequest, MonsterAgentResponse } from '../integrations/MonsterToolsClient';

// Open-AI Standard Interfaces received from Layer 1 (VS Code Copilot UI)
export interface OpenAiChatMessage {
  role: 'system' | 'user' | 'assistant' | 'tool';
  content: string | null;
  name?: string;
  tool_calls?: any[];
}

export interface OpenAiChatCompletionRequest {
  model: string;
  messages: OpenAiChatMessage[];
  temperature?: number;
  top_p?: number;
  stream?: boolean;
  max_tokens?: number;
  tools?: any[];
}

export interface OpenAiChatCompletionResponse {
  id: string;
  object: 'chat.completion';
  created: number;
  model: string;
  choices: {
    index: number;
    message: {
      role: 'assistant';
      content: string;
    };
    finish_reason: 'stop' | 'length' | 'tool_calls';
  }[];
  usage?: {
    prompt_tokens: number;
    completion_tokens: number;
    total_tokens: number;
  };
}

export class LMStudioAdapter {
  
  /**
   * Transforms an incoming OpenAI payload into a flat context prompt tailored for 
   * small local models, extracting tool parameters into a predictable, non-nested map.
   */
  public transformToMonsterRequest(openAiPayload: OpenAiChatCompletionRequest): MonsterAgentRequest {
    if (!openAiPayload || !openAiPayload.messages || openAiPayload.messages.length === 0) {
      throw new Error('[LMStudioAdapter] Cannot transform an empty or missing OpenAI message array.');
    }

    // Flatten message history into an optimized orchestrator prompt for low-compute models
    const aggregatedPrompt = this.compileConversationContext(openAiPayload.messages);
    
    // Extract inference parameters safely into the C# Dictionary structure
    const extractedArguments: Record<string, any> = {
      temperature: openAiPayload.temperature ?? 0.2, // Default cool temperature for strict tool calling behavior
      maxTokens: openAiPayload.max_tokens ?? 1024,
      topP: openAiPayload.top_p ?? 0.95
    };

    // If tools are exposed from VS Code Copilot, attach their definitions as metadata hints
    if (openAiPayload.tools && openAiPayload.tools.length > 0) {
      extractedArguments['toolHints'] = JSON.stringify(openAiPayload.tools);
    }

    return {
      prompt: aggregatedPrompt,
      targetModel: openAiPayload.model || 'ibm/granite-4-h-tiny', // Strict alignment with CodebaseAudit1006 targets
      arguments: extractedArguments
    };
  }

  /**
   * Transforms a structured C# backend execution completion packet back into an 
   * OpenAI standard compliant format expected by the VS Code UI layer.
   */
  public transformToOpenAiResponse(
    monsterResponse: MonsterAgentResponse, 
    originalModel: string
  ): OpenAiChatCompletionResponse {
    return {
      id: `chatcmpl-${monsterResponse.responseId}`,
      object: 'chat.completion',
      created: Math.floor(Date.now() / 1000),
      model: originalModel,
      choices: [
        {
          index: 0,
          message: {
            role: 'assistant',
            content: monsterResponse.rawOutput
          },
          finish_reason: 'stop'
        }
      ],
      usage: {
        prompt_tokens: -1,     // Local runtime reporting abstraction
        completion_tokens: -1,
        total_tokens: -1
      }
    };
  }

  /**
   * Helper utility that folds alternating context lists down into explicit systemic 
   * blocks. This mitigates confusion inside low-compute contexts during long agent loops.
   */
  private compileConversationContext(messages: OpenAiChatMessage[]): string {
    let contextBuilder = '';

    for (const msg of messages) {
      const roleMarker = msg.role.toUpperCase();
      const contentText = msg.content || '';
      
      if (!contentText && msg.tool_calls) {
        // If there's an implicit mid-stream tool loop instruction, stringify it into the model's history text
        contextBuilder += `[CONTEXT - OUTBOUND TOOL_CALL]: ${JSON.stringify(msg.tool_calls)}\n`;
        continue;
      }

      contextBuilder += `[${roleMarker}]: ${contentText}\n`;
    }

    // Append a deterministic trailing delimiter sequence to signal execution handoff to AgentLoop.cs
    contextBuilder += `[SYSTEM DIRECTION]: Evaluate the workspace task using your local deterministic tool executors. Provide output.\n[ASSISTANT]:`;
    
    return contextBuilder;
  }
}
