export class CopilotAdapter {
    static toMonsterTools(body: CopilotRequest): MonsterToolsRequest {
        const prompt =
            body.messages
                ?.map(x => x.content)
                .join("\n") ?? "";

        return {
            prompt,
            stream: body.stream ?? true
        };
    }

    import express from 'express';
import fs from 'fs';
import path from 'path';
import { Readable } from 'stream';
import { SseHandler } from '../streaming/SseHandler.js';

interface ProxyConfig {
  proxy: {
    port: number;
    host: string;
  };
  lmStudio: {
    baseUrl: string;
    defaultModel: string;
    temperature: number;
    stream: boolean;
  };
}

class CopilotAdapter {
  private app: express.Application;
  private config!: ProxyConfig;

  constructor() {
    this.app = express();
    this.loadConfiguration();
    this.setupMiddleware();
    this.setupRoutes();
  }

  /**
   * Safely reads runtime infrastructure profiles from the local proxy configuration file.
   */
  private loadConfiguration(): void {
    try {
      const configPath = path.resolve(process.cwd(), 'config/proxy.json');
      const rawData = fs.readFileSync(configPath, 'utf-8');
      this.config = JSON.parse(rawData) as ProxyConfig;
    } catch (error: any) {
      console.error(`[Initialization Error] Failed to read config/proxy.json: ${error.message}`);
      // Fallback defaults matching your workspace parameters
      this.config = {
        proxy: { port: 3010, host: 'localhost' },
        lmStudio: {
          baseUrl: 'http://localhost:1234/v1',
          defaultModel: 'ibm/granite-4-h-tiny',
          temperature: 0.0,
          stream: true
        }
      };
    }
  }

  /**
   * configures base operational handling layers for incoming network inputs.
   */
  private setupMiddleware(): void {
    this.app.use(express.json());

    // Basic request logger to monitor server proxy traffic
    this.app.use((req, _res, next) => {
      console.log(`[${new Date().toISOString()}] ${req.method} ${req.path}`);
      next();
    });
  }

  /**
   * Sets up endpoints linking your proxy adapter straight to the upstream LM Studio server.
   */
  private setupRoutes(): void {
    // Health status check path
    this.app.get('/health', (_req, res) => {
      res.status(200).json({ status: 'healthy', layer: 'Copilot Compatibility Proxy' });
    });

    // Main streaming completions router pathway for Copilot front-end connections
    this.app.post('/v1/chat/completions', async (req, res) => {
      // Force streaming mode to feed data back through the SseHandler layer
      if (req.body.stream === true || this.config.lmStudio.stream) {
        await SseHandler.handleStream(req, res, async () => {
          const upstreamUrl = `${this.config.lmStudio.baseUrl.replace(/\/$/, '')}/chat/completions`;
          
          const response = await fetch(upstreamUrl, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
              model: req.body.model || this.config.lmStudio.defaultModel,
              messages: req.body.messages,
              temperature: req.body.temperature ?? this.config.lmStudio.temperature,
              stream: true
            })
          });

          if (!response.ok) {
            throw new Error(`LM Studio interface responded with status code: ${response.status}`);
          }

          if (!response.body) {
            throw new Error('LM Studio payload response body context is empty.');
          }

          // Convert standard Web API ReadableStream to Node.js compatibility layer stream descriptor
          return Readable.fromWeb(response.body as any);
        });
      } else {
        // Fallback endpoint block if standard non-stream JSON payloads are ever transmitted
        try {
          const upstreamUrl = `${this.config.lmStudio.baseUrl.replace(/\/$/, '')}/chat/completions`;
          const response = await fetch(upstreamUrl, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
              model: req.body.model || this.config.lmStudio.defaultModel,
              messages: req.body.messages,
              temperature: req.body.temperature ?? this.config.lmStudio.temperature,
              stream: false
            })
          });

          const data = await response.json();
          res.status(response.status).json(data);
        } catch (error: any) {
          res.status(500).json({ error: `Non-stream forwarding crash: ${error.message}` });
        }
      }
    });
  }

  /**
   * Starts up the network interface listener instance.
   */
  public start(): void {
    const { port, host } = this.config.proxy;
    this.app.listen(port, host, () => {
      console.log(`==================================================================`);
      console.log(` MonsterTools Copilot Adapter Proxy active on http://${host}:${port}`);
      console.log(` Target Upstream Backend Point: ${this.config.lmStudio.baseUrl}`);
      console.log(` Active Translation Model Context: ${this.config.lmStudio.defaultModel}`);
      console.log(`==================================================================`);
    });
  }
}

// Fire execution startup sequence instantly when Node invokes the bundle main file
const adapter = new CopilotAdapter();
adapter.start();

}