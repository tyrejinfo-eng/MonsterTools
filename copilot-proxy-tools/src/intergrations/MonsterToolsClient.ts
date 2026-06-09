import { z } from 'zod';
import fs from 'fs';
import path from 'path';

// Define structural schemas for structural tool execution
export const ToolRequestSchema = z.object({
  toolName: z.string(),
  arguments: z.record(z.any()),
  contextId: z.string().uuid()
});

export const ToolResultSchema = z.object({
  success: z.boolean(),
  output: z.string(),
  error: z.string().optional()
});

export class MonsterToolsClient {
  private engineUrl: string;

  constructor() {
    const configPath = path.resolve(process.cwd(), 'config/proxy.json');
    const config = JSON.parse(fs.readFileSync(configPath, 'utf-8'));
    this.engineUrl = config.monsterToolsEngine.endpoint;
  }

  // Strictly validates schema parameters before processing execution down pipeline
  async executeTool(rawRequest: unknown): Promise<z.infer<typeof ToolResultSchema>> {
    const parsedRequest = ToolRequestSchema.safeParse(rawRequest);
    
    if (!parsedRequest.success) {
      return {
        success: false,
        error: `Schema validation failed: ${parsedRequest.error.message}`
      };
    }

    try {
      const response = await fetch(this.engineUrl, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(parsedRequest.data)
      });

      if (!response.ok) {
        throw new Error(`Execution layer responded with status ${response.status}`);
      }

      const rawResult = await response.json();
      return ToolResultSchema.parse(rawResult);
    } catch (err: any) {
      return {
        success: false,
        error: `Pipeline execution failed: ${err.message}`
      };
    }
  }
}
