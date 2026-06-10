import { Transform, TransformCallback } from 'stream';

export class ChunkTransformer extends Transform {
    private bufferAccumulator: string = '';

    constructor() {
        super({ objectMode: true });
    }

    _transform(chunk: any, encoding: string, callback: TransformCallback): void {
        // Append raw binary/string network stream packets to our safe accumulator
        this.bufferAccumulator += chunk.toString('utf8');
        
        let breakIndex: number;
        // Process every complete string break found inside our data buffer
        while ((breakIndex = this.bufferAccumulator.indexOf('\n')) !== -1) {
            const rawLine = this.bufferAccumulator.substring(0, breakIndex).trim();
            this.bufferAccumulator = this.bufferAccumulator.substring(breakIndex + 1);

            if (!rawLine) continue;
            if (!rawLine.startsWith('data:')) {
                // Pass non-data stream payloads directly along the pipeline
                this.push(rawLine);
                continue;
            }

            const cleanJsonPayload = rawLine.replace(/^data:\s*/, '');
            if (cleanJsonPayload === '[DONE]') {
                this.push({ done: true });
                continue;
            }

            try {
                // Ensure complete brace matching before evaluating content
                if (cleanJsonPayload.startsWith('{') && cleanJsonPayload.endsWith('}')) {
                    const parsedData = JSON.parse(cleanJsonPayload);
                    this.push(parsedData);
                } else {
                    // Stash unaligned text blocks back inside our accumulator
                    this.bufferAccumulator = rawLine + '\n' + this.bufferAccumulator;
                    break;
                }
            } catch (jsonError) {
                // Retain corrupted chunks for the next incoming data packet instead of crashing
                this.bufferAccumulator = rawLine + '\n' + this.bufferAccumulator;
                break;
            }
        }
        callback();
    }
}
