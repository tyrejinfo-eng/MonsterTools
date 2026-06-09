import { Request, Response } from 'express';
import { Readable } from 'stream';
import { ChunkTransformer } from './ChunkTransformer.js';

export class SseHandler {
  /**
   * Express route handler to stream LLM outputs from LM Studio to the client via SSE.
   * @param getUpstreamStream A function that returns the raw Readable network stream from LM Studio/Upstream.
   */
  public static async handleStream(
    req: Request,
    res: Response,
    getUpstreamStream: () => Promise<Readable>
  ): Promise<void> {
    // 1. Establish strict SSE headers required by the client/Copilot interface
    res.writeHead(200, {
      'Content-Type': 'text/event-stream',
      'Cache-Control': 'no-cache, no-transform',
      'Connection': 'keep-alive',
      'X-Accel-Buffering': 'no' // Prevents reverse proxies like Nginx from buffering the stream
    });

    // Send initial flush to open the pipeline immediately
    res.write(': ok\n\n');

    let transformer: ChunkTransformer | null = new ChunkTransformer();
    let upstreamStream: Readable | null = null;

    try {
      // 2. Fetch the live connection stream from LM Studio
      upstreamStream = await getUpstreamStream();

      // 3. Pipe raw buffer fragments through the transformer
      upstreamStream.pipe(transformer);

      // 4. Read cleanly formatted objects from the transformer and emit them as pure SSE text chunks
      transformer.on('data', (data: { done: boolean; content: string }) => {
        if (data.done) {
          res.write('data: [DONE]\n\n');
          res.end();
          return;
        }

        // Construct standard SSE data package
        res.write(`data: ${JSON.stringify({ content: data.content })}\n\n`);
      });

      // Handle downstream closure or parsing failures
      transformer.on('error', (err) => {
        res.write(`data: ${JSON.stringify({ error: 'Stream parsing failure occurred.' })}\n\n`);
        res.end();
      });

      upstreamStream.on('error', (err) => {
        res.write(`data: ${JSON.stringify({ error: 'Upstream connection dropped.' })}\n\n`);
        res.end();
      });

    } catch (error: any) {
      // Handle failures before the stream could successfully initialize
      res.write(`data: ${JSON.stringify({ error: `Failed to initialize stream: ${error.message}` })}\n\n`);
      res.end();
    }

    // 5. Clean up open descriptors instantly if the client disconnects or closes the tab
    req.on('close', () => {
      if (upstreamStream) {
        upstreamStream.unpipe();
        upstreamStream.destroy();
      }
      if (transformer) {
        transformer.removeAllListeners();
        transformer.destroy();
        transformer = null;
      }
    });
  }
}
