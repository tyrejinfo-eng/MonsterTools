import { Transform, TransformCallback } from 'stream';

interface SSETokenDelta {
    choices?: Array<{
        delta?: {
            content?: string;
            role?: string;
        };
        finish_reason?: string | null;
    }>;
}

/**
 * A stateful transform stream handler that safely buffers and processes incoming 
 * Server-Sent Event (SSE) tokens from local LLM engines without throwing parsing crashes.
 */
export class ChunkTransformer extends Transform {
    // Retains partial line fragments between raw network buffer emissions
    private stringBuffer: string = '';

    constructor() {
        super({ objectMode: true });
    }

    /**
     * Intercepts and reconstructs streaming chunks smoothly.
     */
    override _transform(chunk: any, encoding: string, callback: TransformCallback): void {
        try {
            // Append incoming binary or text data into our persistent string buffer
            this.stringBuffer += Buffer.isBuffer(chunk) ? chunk.toString('utf8') : chunk;

            let lineBreakIndex: number;
            
            // Loop while complete line slices exist within our current stream accumulation window
            while ((lineBreakIndex = this.stringBuffer.indexOf('\n')) !== -1) {
                // Isolate the line string and pull out any carriage return markers (\r)
                let rawLine = this.stringBuffer.substring(0, lineBreakIndex).replace(/\r/g, '').trim();
                
                // Advance the persistent buffer window forward past our consumed breakpoint
                this.stringBuffer = this.stringBuffer.substring(lineBreakIndex + 1);

                // Ignore empty heartbeats or spacer lines common in SSE protocols
                if (!rawLine) {
                    continue;
                }

                // Check for the standard OpenAI terminal stream marker
                if (rawLine === 'data: [DONE]') {
                    this.push({ done: true, raw: 'data: [DONE]\n\n' });
                    continue;
                }

                // Process lines containing valid data prefixes
                if (rawLine.startsWith('data:')) {
                    // Extract the raw JSON content block following the SSE marker
                    const jsonPayloadString = rawLine.substring(5).trim();

                    if (!jsonPayloadString) {
                        continue;
                    }

                    // Ensure basic brace configuration integrity prior to executing JSON compilation
                    if (jsonPayloadString.startsWith('{') && jsonPayloadString.endsWith('}')) {
                        try {
                            const parsedTokenData = JSON.parse(jsonPayloadString) as SSETokenDelta;
                            
                            // Forward the structured object for tool argument check mapping, along with the raw payload
                            this.push({
                                done: false,
                                data: parsedTokenData,
                                raw: `${rawLine}\n\n`
                            });
                        } catch (parseError) {
                            // If an internal string extraction fragment fails parsing, skip it rather than crashing the proxy
                            continue;
                        }
                    } else {
                        // If the line has a data prefix but incomplete braces, it was split across chunks.
                        // Reconstruct the line segment back into the buffer and await more network data.
                        this.stringBuffer = rawLine + '\n' + this.stringBuffer;
                        break;
                    }
                } else {
                    // Pass un-prefixed metadata segments straight down the transform chain
                    this.push({ done: false, data: null, raw: `${rawLine}\n` });
                }
            }
            callback();
        } catch (fatalTransformError) {
            // Protect the active pipeline runtime by surfacing errors cleanly through standard streams
            callback(fatalTransformError as Error);
        }
    }

    /**
     * Flushes out any remaining string fragments when the upstream source shuts down.
     */
    override _flush(callback: TransformCallback): void {
        const remainingData = this.stringBuffer.trim();
        if (remainingData && remainingData.startsWith('data:')) {
            const jsonPayloadString = remainingData.substring(5).trim();
            if (jsonPayloadString.startsWith('{') && jsonPayloadString.endsWith('}')) {
                try {
                    const parsedTokenData = JSON.parse(jsonPayloadString);
                    this.push({ done: false, data: parsedTokenData, raw: `${remainingData}\n\n` });
                } catch {
                    // Suppress trailing parse noise gracefully
                }
            }
        }
        callback();
    }
}
