
import express from 'express';
import { LMStudioAdapter } from './adapters/LMStudioAdapter.js';
import { SseHandler } from './streaming/SseHandler.js';
import { ProxyStartupService } from './startup/ProxyStartupService.js';
import { MonsterToolsClient } from './integrations/MonsterToolsClient.js';

const config = ProxyStartupService.loadConfig();
const app = express();

const adapter = new LMStudioAdapter(
  new MonsterToolsClient(config.monsterTools.url),
  `${config.lmStudio.url.replace(/\/$/, '')}/v1`,
  config.lmStudio.model
);

app.use(express.json({ limit: '8mb' }));

app.get('/health', (_req, res) => {
  res.json({
    ok: true,
    service: 'copilot-proxy-tools',
    proxy: config.proxy,
    monsterTools: config.monsterTools.url,
    lmStudio: config.lmStudio.url
  });
});

app.post('/v1/chat/completions', async (req, res) => {
  try {
    const stream = await adapter.transformAndRouteRequest(req.body);
    SseHandler.streamResponseToClient(res, stream);
  } catch (error: any) {
    res.status(500).json({ error: error.message });
  }
});

app.listen(config.proxy.port, config.proxy.host, () => {
  console.log(`copilot-proxy-tools listening on http://${config.proxy.host}:${config.proxy.port}`);
  console.log(`MonsterTools -> ${config.monsterTools.url}`);
  console.log(`LM Studio -> ${config.lmStudio.url}`);
});
