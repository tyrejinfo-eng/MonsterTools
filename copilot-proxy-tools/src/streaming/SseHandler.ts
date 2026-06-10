import { Response } from 'express';
import { Readable } from 'stream';
import { ChunkTransformer } from './ChunkTransformer';

/**
 * Orchestrates multi-turn token streaming delivery from our proxy core directly 
 * to the connected VS Code extension UI interface.
 */
export class SseHandler {
    /**
     * Safely attaches a streaming model response to an active Express client context.
     */
    public static streamResponseToClient(expressResponse: Response, incomingUpstreamNodeStream: Readable): void {
        // Enforce the standard headers required for Server-Sent Events (SSE)
        expressResponse.writeHead(200, {
            'Content-Type': 'text/event-stream',
            'Cache-Control': 'no-cache',
            'Connection': 'keep-alive',
            'X-Accel-Buffering': 'no' // Prevents Nginx/edge proxies from buffering tokens
        });

        const streamTransformer = new ChunkTransformer();

        // Pipe the raw incoming data stream directly through our stateful transformer line-buffer
        incomingUpstreamNodeStream.pipe(streamTransformer);

        // Process buffered tokens as they finish parsing
        streamTransformer.on('data', (payload: { done: boolean; data: any; raw: string }) => {
            if (payload.raw) {
                // Write the clean string directly out to the open IDE network socket
                expressResponse.write(payload.raw);
            }
        });

        streamTransformer.on('error', (streamError: Error) => {
            console.error('Fatal crash intercepted within active stream transformer engine context:', streamError.message);
            if (!expressResponse.headersSent) {
                expressResponse.write('data: {"error": "Internal streaming transformation loop exception occurred."}\n\n');
            }
            expressResponse.end();
        });

        streamTransformer.on('end', () => {
            // Safely close the network connection context once the stream completes
            expressResponse.end();
        });

        // Clean up resources if the developer cancels the request early inside VS Code
        expressResponse.on('close', () => {
            incomingUpstreamNodeStream.destroy();
            streamTransformer.destroy();
        });
    }
}
