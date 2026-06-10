import { Request, Response, Router } from 'express';
import { LMStudioAdapter, OpenAiChatCompletionRequest } from '../adapters/LMStudioAdapter';
import { MonsterToolsClient } from '../integrations/MonsterToolsClient';
import { ChunkTransformer } from '../streaming/ChunkTransformer';
import { SseHandler } from '../streaming/SseHandler'; // Assuming existing SSE helper structure

export const ChatRoute = Router();

// Instantiate the integration adapters and backend clients
const adapter = new LMStudioAdapter();
const monsterClient = new MonsterToolsClient();

/**
 * POST /v1/chat/completions
 * Main interceptor route that fools the VS Code GitHub Copilot extension 
 * into using your local MonsterTools engine pipeline instead of cloud endpoints.
 */
ChatRoute.post('/v1/chat/completions', async (req: Request, res: Response): Promise<void> => {
  const openAiBody = req.body as OpenAiChatCompletionRequest;

  // 1. Validation Check
  if (!openAiBody || !openAiBody.messages) {
    res.status(400).json({
      error: {
        message: 'Invalid OpenAI format payload. Messages array is required.',
        type: 'invalid_request_error',
        code: 'missing_required_parameter'
      }
    });
    return;
  }

  // 2. Route Streaming vs Blocking Implementations
  if (openAiBody.stream) {
    handleStreamingChat(openAiBody, res);
  } else {
    await handleBlockingChat(openAiBody, res);
  }
});

/**
 * Handles non-streaming execution requests (Deterministic Tool Evaluation Paths)
 */
async function handleBlockingChat(openAiBody: OpenAiChatCompletionRequest, res: Response): Promise<void> {
  try {
    // Translate standard cloud formats into flat C# orchestrator requests
    const monsterRequest = adapter.transformToMonsterRequest(openAiBody);

    // Dispatch downstream to the Minimal API on Program.cs (Port 5105)
    const monsterResponse = await monsterClient.dispatchAgentTask(monsterRequest);

    // Format the returned tool metrics back into expected OpenAI chat definitions
    const openAiResponse = adapter.transformToOpenAiResponse(monsterResponse, openAiBody.model);

    res.status(200).json(openAiResponse);
  } catch (error: any) {
    console.error('[ChatRoute][Blocking] Failed processing through the C# Agent loop:', error.message);
    res.status(502).json({
      error: {
        message: `Upstream Layer 3 processing error: ${error.message}`,
        type: 'api_error',
        code: 'bad_gateway'
      }
    });
  }
}

/**
 * Handles live code auto-completion or chat response streaming with Token Buffer protection
 */
function handleStreamingChat(openAiBody: OpenAiChatCompletionRequest, res: Response): void {
  try {
    // Set headers matching Server-Sent Events (SSE) requirements
    res.setHeader('Content-Type', 'text/event-stream');
    res.setHeader('Cache-Control', 'no-cache');
    res.setHeader('Connection', 'keep-alive');

    // Translate the payload
    const monsterRequest = adapter.transformToMonsterRequest(openAiBody);

    // Initialise your safe token buffer transformer to protect against mid-packet brace truncations
    const safeBufferTransformer = new ChunkTransformer();

    // Pipe the safe output directly to the VS Code client response stream
    safeBufferTransformer.pipe(res);

    // Mock/Simulated SSE Token Distribution Loop for the local model pipeline tracking
    // Note: When integrating direct streaming from LMStudioService.cs, pipe its readable stream 
    // directly into `safeBufferTransformer` here.
    monsterClient.dispatchAgentTask(monsterRequest)
      .then((monsterResponse) => {
        const fullOutput = monsterResponse.rawOutput;
        const words = fullOutput.split(/(\s+)/); // Keep whitespace fragments intact

        let tokenIndex = 0;

        function streamNextToken() {
          if (tokenIndex >= words.length) {
            // Signal streaming completion per open specification formats
            safeBufferTransformer.write(`data: [DONE]\n\n`);
            safeBufferTransformer.end();
            return;
          }

          const token = words[tokenIndex];
          tokenIndex++;

          // Build a compliant incremental OpenAI chunk
          const sseChunk = {
            id: `chatcmpl-${monsterResponse.responseId}`,
            object: 'chat.completion.chunk',
            created: Math.floor(Date.now() / 1000),
            model: openAiBody.model,
            choices: [
              {
                index: 0,
                delta: { content: token },
                finish_reason: null
              }
            ]
          };

          // Write explicitly down the transformation buffer chain
          safeBufferTransformer.write(`data: ${JSON.stringify(sseChunk)}\n\n`);

          // Fast throttling pacing for local interface responsiveness
          setTimeout(streamNextToken, 15);
        }

        // Start token emission
        streamNextToken();
      })
      .catch((error) => {
        safeBufferTransformer.emit('error', error);
      });

    // Handle connection drops from the editor layer gracefully
    req.on('close', () => {
      safeBufferTransformer.destroy();
    });

  } catch (error: any) {
    console.error('[ChatRoute][Streaming] Exception while configuring streaming pipeline:', error.message);
    res.write(`data: ${JSON.stringify({ error: error.message })}\n\n`);
    res.end();
  }
}
