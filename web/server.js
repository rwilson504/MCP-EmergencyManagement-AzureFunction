// Lightweight Express static server for SPA hosting on Linux App Service
// Use dynamic imports so we can emit diagnostics if dependencies were not installed.
import path from 'path';
import { fileURLToPath } from 'url';

async function bootstrap() {
  const __filename = fileURLToPath(import.meta.url);
  const __dirname = path.dirname(__filename);

  // Basic filesystem diagnostics
  try {
    const fs = await import('fs');
    const candidates = [
      path.join(__dirname, 'package.json'),
      path.join(__dirname, 'node_modules'),
      path.join(__dirname, 'node_modules', 'express'),
      path.join(__dirname, 'dist', 'index.html')
    ];
    for (const c of candidates) {
      fs.existsSync(c)
        ? console.log(`[startup] exists: ${c}`)
        : console.warn(`[startup] missing: ${c}`);
    }
  } catch (e) {
    console.warn('[startup] fs diagnostics failed', e);
  }

  const needed = ['express', '@azure/identity'];
  const loaded = {};
  for (const m of needed) {
    try {
      // eslint-disable-next-line no-await-in-loop
      loaded[m] = await import(m);
      console.log(`[startup] module ok: ${m}`);
    } catch (e) {
      console.error(`[startup] module MISSING: ${m}`);
      console.error(`[startup] FATAL: Cannot continue without '${m}'. Ensure npm install ran (SCM_DO_BUILD_DURING_DEPLOYMENT=true) and package.json is present.`);
      return; // Abort startup; App Service will restart after logs are captured.
    }
  }

  const express = loaded['express'].default;
  const { DefaultAzureCredential } = loaded['@azure/identity'];
  const app = express();
  const credential = new DefaultAzureCredential();

  const distPath = path.join(__dirname, 'dist');
  app.use(express.static(distPath, { maxAge: '1d', index: 'index.html' }));

  // Runtime config script to expose telemetry + public API base URL to the SPA at runtime
  app.get('/_appconfig.js', (_req, res) => {
    let cs = process.env.APPLICATIONINSIGHTS_CONNECTION_STRING || '';
    let ikey = process.env.APPINSIGHTS_INSTRUMENTATIONKEY || '';
    let apiBase = process.env.API_BASE_URL || '';
    let mapsClientId = process.env.AZURE_MAPS_CLIENT_ID || '';

    const trimQuotes = (v) => v.replace(/^"+|"+$/g, '');
    cs = trimQuotes(cs.trim());
    ikey = trimQuotes(ikey.trim());
    apiBase = trimQuotes(apiBase.trim());
    mapsClientId = trimQuotes(mapsClientId.trim());
    if (apiBase) apiBase = apiBase.replace(/\/+$/, '');
    if (!cs && ikey) cs = `InstrumentationKey=${ikey}`;

    const payloadLines = [
      '// Runtime injected config (telemetry + API base + Azure Maps)',
      `window.__APPINSIGHTS_INSTRUMENTATION_KEY__ = ${JSON.stringify(ikey)};`,
      `window.__APPINSIGHTS_CONNECTION_STRING__ = ${JSON.stringify(cs)};`,
      `window.__API_BASE_URL__ = ${JSON.stringify(apiBase)};`,
      `window.__AZURE_MAPS_CLIENT_ID__ = ${JSON.stringify(mapsClientId)};`
    ];
    res.type('application/javascript').send(payloadLines.join('\n'));
  });

  app.get('/api/maps-token', async (_req, res) => {
    try {
      const token = await credential.getToken('https://atlas.microsoft.com/.default');
      if (!token) {
        return res.status(500).json({ error: 'Failed to acquire token' });
      }
      return res.json({
        access_token: token.token,
        expires_on: Math.floor(token.expiresOnTimestamp / 1000),
        token_type: 'Bearer'
      });
    } catch (err) {
      console.error('Azure Maps token acquisition failed', err);
      return res.status(500).json({ error: 'Token acquisition failed', message: err instanceof Error ? err.message : 'Unknown error' });
    }
  });

  app.get('*', (_req, res) => {
    res.sendFile(path.join(distPath, 'index.html'));
  });

  const port = process.env.PORT || 8080;
  app.listen(port, () => {
    console.log(`Web SPA listening on port ${port}`);
  });
}

bootstrap().catch(err => {
  console.error('[startup] bootstrap failure', err);
});
