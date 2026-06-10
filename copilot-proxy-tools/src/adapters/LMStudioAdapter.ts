import { Readable } from 'stream';
import axios from 'axios';

interface OpenAiMessage {
    role: 'system' | 'user' | 'assistant';
    content: string;
}

interface OpenAiChatPayload {
    model: string;
    messages: OpenAiMessage[];
    temperature?: number;
    stream?: boolean;
}

interface CsharpAgentRequest {
    prompt: string;
    targetModel: string;
    arguments: Record<string, any>;
}

interface CsharpAgentResponse {
    responseId: string;
    rawOutput: string;
    status: string;
    timestamp: string;
}

/**
 * Maps incoming IDE extension payloads to the downstream C# orchestration agent 
 * and local inference runtimes.
 */
export class LMStudioAdapter {
    private readonly csharpEngineUrl: string;
    private readonly lmStudioEndpoint: string;

    constructor(
        csharpEngineUrl: string = 'http://127.0.0',
        lmStudioEndpoint: string = 'http://127.0.0'
    ) {
        this.csharpEngineUrl = csharpEngineUrl;
        this.lmStudioEndpoint = lmStudioEndpoint;
    }

    /**
     * Transforms chat completion payloads and routes them through the C# agent loop or LM Studio.
     */
    public async transformAndRouteRequest(payload: OpenAiChatPayload): Promise<Readable> {
        if (!payload.messages || payload.messages.length === 0) {
            throw new Error('Malformed execution payload: Request message history is empty.');
        }

        // Extract the latest developer command from the message history
        const latestUserMessage = [...payload.messages].reverse().find(msg => msg.role === 'user');
        const corePrompt = latestUserMessage ? latestUserMessage.content : '';

        // Check if the prompt requires native execution tools like compilation or filesystem updates
        const requiresDeterministicTools = /(build|compile|write file|generate file|create code|run project)/i.test(corePrompt);

        if (requiresDeterministicTools) {
            return this.dispatchToCsharpOrchestrator(corePrompt, payload.model);
        }

        // Fall back to a standard streaming pass-through for conversational text requests
        return this.dispatchDirectStreamToLmStudio(payload);
    }

    /**
     * Packages structural developer intents into an AgentRequestContext and executes the C# loop.
     */
    private async dispatchToCsharpOrchestrator(prompt: string, modelName: string): Promise<Readable> {
        try {
            const requestPayload: CsharpAgentRequest = {
                prompt: prompt,
                targetModel: modelName,
                arguments: {
                    workspaceRoot: process.cwd(),
                    timestamp: new Date().toISOString()
                }
            };

            // Post the structural request to the ASP.NET Minimal API endpoint
            const response = await axios.post<CsharpAgentResponse>(this.csharpEngineUrl, requestPayload, {
                headers: { 'Content-Type': 'application/json' },
                timeout: 120000 // Match the 120s server timeout allocation
            });

            const agentOutputText = response.data.rawOutput || 'Task execution completed successfully with no returned logs.';
            
            // Format the static C# result block into a compliant OpenAI SSE token stream
            return this.convertTextToSseStream(agentOutputText, modelName);
        } catch (error: any) {
            const errorMessage = error.response?.data?.error || error.message;
            return this.convertTextToSseStream(`Error: The C# compilation orchestrator failed. Context: ${errorMessage}`, modelName);
        }
    }

    /**
     * Streams incoming tokens directly from the local inference engine back to the client proxy.
     */
    private async dispatchDirectStreamToLmStudio(payload: OpenAiChatPayload): Promise<Readable> {
        const targetUrl = `${this.lmStudioEndpoint}/chat/completions`;
        
        const response = await axios.post(targetUrl, payload, {
            responseType: 'stream',
            headers: { 'Content-Type': 'application/json' }
        });

        return response.data as Readable;
    }

    /**
     * Formats a raw text block into an OpenAI-compliant Server-Sent Event (SSE) token stream.
     */
    private convertTextToSseStream(text: string, modelName: string): Readable {
        const stream = new Readable({ read() {} });
        const streamId = `chatcmpl-${Math.random().toString(36).substring(2, 11)}`;
        
        // Escape special characters to maintain clean JSON formatting in transit
        const safeText = JSON.stringify(text).slice(1, -1);

        const dataChunk = {
            id: streamId,
            object: 'chat.completion.chunk',
            created: Math.floor(Date.now() / 1000),
            model: modelName,
            choices: [{
                index: 0,
                delta: { content: text },
                finish_reason: null
            }]
        };

        const stopChunk = {
            id: streamId,
            object: 'chat.completion.chunk',
            created: Math.floor(Date.now() / 1000),
            model: modelName,
            choices: [{
                index: 0,
                delta: {},
                finish_reason: 'stop'
            }]
        };

        // Write standard OpenAI SSE protocol blocks out to the active transformer pipeline
        stream.push(`data: ${JSON.stringify(dataChunk)}\n\n`);
        stream.push(`data: ${JSON.stringify(stopChunk)}\n\n`);
        stream.push('data: [DONE]\n\n');
        stream.push(null); // Signal the end of the readable stream source

        return stream;
    }
}
