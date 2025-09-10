// Lightweight Express static server for SPA hosting on Linux App Service
import express from 'express';
import path from 'path';
import { fileURLToPath } from 'url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const app = express();

const distPath = path.join(__dirname, 'dist');
app.use(express.static(distPath, { maxAge: '1d', index: 'index.html' }));

// Runtime config script to expose telemetry + public API base URL to the SPA at runtime
app.get('/_appconfig.js', (_req, res) => {
  let cs = process.env.APPLICATIONINSIGHTS_CONNECTION_STRING || '';
  let ikey = process.env.APPINSIGHTS_INSTRUMENTATIONKEY || '';
  let apiBase = process.env.API_BASE_URL || '';

  // Trim wrapping quotes if any (defensive for mis-set app settings)
  const trimQuotes = (v) => v.replace(/^"+|"+$/g, '');
  cs = trimQuotes(cs.trim());
  ikey = trimQuotes(ikey.trim());
  apiBase = trimQuotes(apiBase.trim());

  // Normalize apiBase: remove trailing slashes
  if (apiBase) {
    apiBase = apiBase.replace(/\/+$/, '');
  }

  // Synthesize connection string from instrumentation key if needed (older portal badge scenario)
  if (!cs && ikey) {
    cs = `InstrumentationKey=${ikey}`;
  }

  const payloadLines = [
    '// Runtime injected config (telemetry + API base)',
    `window.__APPINSIGHTS_INSTRUMENTATION_KEY__ = ${JSON.stringify(ikey)};`,
    `window.__APPINSIGHTS_CONNECTION_STRING__ = ${JSON.stringify(cs)};`,
    `window.__API_BASE_URL__ = ${JSON.stringify(apiBase)};`
  ];
  res.type('application/javascript').send(payloadLines.join('\n'));
});

// SPA fallback
app.get('*', (_req, res) => {
  res.sendFile(path.join(distPath, 'index.html'));
});

const port = process.env.PORT || 8080;
app.listen(port, () => {
  console.log(`Web SPA listening on port ${port}`);
});
