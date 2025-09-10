// Lightweight Express static server for SPA hosting on Linux App Service
import express from 'express';
import path from 'path';
import { fileURLToPath } from 'url';
import { DefaultAzureCredential } from '@azure/identity';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
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

  // Trim wrapping quotes if any (defensive for mis-set app settings)
  const trimQuotes = (v) => v.replace(/^"+|"+$/g, '');
  cs = trimQuotes(cs.trim());
  ikey = trimQuotes(ikey.trim());
  apiBase = trimQuotes(apiBase.trim());
  mapsClientId = trimQuotes(mapsClientId.trim());

  // Normalize apiBase: remove trailing slashes
  if (apiBase) {
    apiBase = apiBase.replace(/\/+$/, '');
  }

  // Synthesize connection string from instrumentation key if needed (older portal badge scenario)
  if (!cs && ikey) {
    cs = `InstrumentationKey=${ikey}`;
  }

  const payloadLines = [
    '// Runtime injected config (telemetry + API base + Azure Maps)',
    `window.__APPINSIGHTS_INSTRUMENTATION_KEY__ = ${JSON.stringify(ikey)};`,
    `window.__APPINSIGHTS_CONNECTION_STRING__ = ${JSON.stringify(cs)};`,
    `window.__API_BASE_URL__ = ${JSON.stringify(apiBase)};`,
    `window.__AZURE_MAPS_CLIENT_ID__ = ${JSON.stringify(mapsClientId)};`
  ];
  res.type('application/javascript').send(payloadLines.join('\n'));
});

// Server-side token acquisition for Azure Maps using Managed Identity
// This replaces the previous Function-based maps-token endpoint, keeping the token flow entirely within the web tier.
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

// SPA fallback
app.get('*', (_req, res) => {
  res.sendFile(path.join(distPath, 'index.html'));
});

const port = process.env.PORT || 8080;
app.listen(port, () => {
  console.log(`Web SPA listening on port ${port}`);
});
