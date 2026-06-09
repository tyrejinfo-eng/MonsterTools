import { Transform, TransformCallback } from 'stream';

/**
 * Maps the actual incoming JSON structure from an OpenAI/LM-Studio compatible event stream.
 */
interface LMStudioStreamPayload {
  choices?: Array<{
    delta?: {
      content?: string;
      role?: string;
    };
    finish_reason?: string | null;
  }>;
}

export class ChunkTransformer extends Transform {
  private buffer: string = '';

  constructor() {
    // We ingest raw Buffers/Strings from the network and output parsed stream objects
    super({ readableObjectMode: true, writableObjectMode: false });
  }

  /**
   * Chunks incoming raw text streaming packets, safely handling split lines.
   */
  _transform(chunk: any, encoding: string, callback: TransformCallback): void {
    // Append incoming data slice to internal buffer
    this.buffer += chunk.toString('utf-8');
    
    const lines = this.buffer.split('\n');
    // Retain the last incomplete line fragment in the buffer
    this.buffer = lines.pop() || '';

    for (const line of lines) {
      const trimmed = line.trim();
      
      if (!trimmed) continue;
      
      // Look for the standard terminal stream marker
      if (trimmed === 'data: [DONE]') {
        this.push({ done: true, content: '' });
        continue;
      }

      if (trimmed.startsWith('data: ')) {
        try {
          const rawJson = trimmed.slice(6);
          const parsed = JSON.parse(rawJson) as LMStudioStreamPayload;
          const content = parsed.choices?.[0]?.delta?.content;
          
          if (content !== undefined && content !== null) {
            this.push({
              done: false,
              content: content
            });
          }
        } catch (err) {
          // Skip lines that are incomplete or temporarily malformed without crashing the stream
          continue;
        }
      }
    }
    callback();
  }

  /**
   * Sweeps and flushes any trailing payload elements remaining when the socket closes.
   */
  _flush(callback: TransformCallback): void {
    const remaining = this.buffer.trim();
    if (remaining.startsWith('data: ') && remaining !== 'data: [DONE]') {
      try {
        const rawJson = remaining.slice(6);
        const parsed = JSON.parse(rawJson) as LMStudioStreamPayload;
        const content = parsed.choices?.[0]?.delta?.content;
        if (content) {
          this.push({ done: false, content });
        }
      } catch (e) {
        // Suppress trailing parse errors on exit
      }
    }
    this.push({ done: true, content: '' });
    callback();
  }
}
