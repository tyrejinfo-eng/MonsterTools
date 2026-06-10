import { Transform, TransformCallback } from 'stream';

export interface SSEMessage {
  id?: string;
  event?: string;
  data: string;
}

export class ChunkTransformer extends Transform {
  // Safe sticky string buffer to accumulate incomplete network packets
  private buffer: string = '';

  constructor() {
    // Force object operating mode so downstream consumers receive structured types
    super({ readableObjectMode: true, writableObjectMode: false });
  }

  /**
   * Transforms incoming raw byte chunks from the LLM runtime into verified textual tokens.
   */
  public _transform(chunk: any, encoding: BufferEncoding, callback: TransformCallback): void {
    try {
      // Append the incoming packet fragment into our sticky tracking buffer
      const dataString = Buffer.isBuffer(chunk) ? chunk.toString('utf8') : String(chunk);
      this.buffer += dataString;

      // Split chunks on clean line breaks to evaluate complete Server-Sent Event (SSE) sequences
      let lines = this.buffer.split(/\r?\n/);

      // Keep the final line segment in the buffer since it may be a partial data slice
      this.buffer = lines.pop() || '';

      let currentEvent: Partial<SSEMessage> = {};

      for (const line of lines) {
        const trimmed = line.trim();

        // Skip empty keep-alive pings
        if (!trimmed) {
          continue;
        }

        // Handle SSE comment lines
        if (trimmed.startsWith(':')) {
          continue;
        }

        // Parse SSE field descriptors
        const fieldSeparatorIndex = trimmed.indexOf(':');
        if (fieldSeparatorIndex === -1) {
          continue; 
        }

        const field = trimmed.slice(0, fieldSeparatorIndex).trim();
        let value = trimmed.slice(fieldSeparatorIndex + 1);
        
        // Remove exactly one leading space from the value as per the SSE specification
        if (value.startsWith(' ')) {
          value = value.slice(1);
        }

        switch (field) {
          case 'id':
            currentEvent.id = value;
            break;
          case 'event':
            currentEvent.event = value;
            break;
          case 'data':
            // Accumulate multi-line data fields safely
            currentEvent.data = currentEvent.data ? `${currentEvent.data}\n${value}` : value;
            break;
        }

        // If a complete data message payload has been constructed, validate and push it down the pipe
        if (field === 'data' && currentEvent.data) {
          this.processDataPayload(currentEvent as SSEMessage);
          currentEvent = {}; // Clear tracker context for the next token packet
        }
      }

      callback();
    } catch (error) {
      // Emit a clean error event to prevent catastrophic unhandled proxy process crashes
      callback(error instanceof Error ? error : new Error(String(error)));
    }
  }

  /**
   * Finalises any trailing fragments left inside the memory stack when the connection closes.
   */
  public _flush(callback: TransformCallback): void {
    try {
      const remaining = this.buffer.trim();
      
      // Attempt a last-ditch parse if a non-empty string sequence was left hanging at stream end
      if (remaining && remaining.startsWith('data:')) {
        const value = remaining.replace(/^data:\s*/, '');
        this.processDataPayload({ data: value });
      }
      
      callback();
    } catch (error) {
      callback(error instanceof Error ? error : new Error(String(error)));
    }
  }

  /**
   * Deep structural evaluation loop to guarantee complete, unbroken JSON objects.
   */
  private processDataPayload(message: SSEMessage): void {
    const rawData = message.data.trim();

    // Pass standard stream markers down immediately without parsing JSON
    if (rawData === '[DONE]') {
      this.push({ ...message, data: rawData });
      return;
    }

    // Check balancing braces to see if this is an absolute JSON snippet candidate
    if (rawData.startsWith('{') && rawData.endsWith('}')) {
      if (this.isValidJson(rawData)) {
        this.push({ ...message, data: JSON.parse(rawData) });
      } else {
        // Drop safely or forward as plaintext if the schema verification fails
        this.emit('warn', `Malformed json brace structure discarded from stream pipeline: ${rawData}`);
      }
    } else {
      // If it's a plaintext token stream from standard output fields, bypass parsing checks
      this.push(message);
    }
  }

  /**
   * Validates if the string is complete and safe to parse without throwing.
   */
  private isValidJson(str: string): boolean {
    try {
      JSON.parse(str);
      return true;
    } catch {
      return false;
    }
  }
}
