// Lightweight Express static server for SPA hosting on Linux App Service
import express from 'express';
import path from 'path';
import { fileURLToPath } from 'url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const app = express();

const distPath = path.join(__dirname, 'dist');
app.use(express.static(distPath, { maxAge: '1d', index: 'index.html' }));

// SPA fallback
app.get('*', (_req, res) => {
  res.sendFile(path.join(distPath, 'index.html'));
});

const port = process.env.PORT || 8080;
app.listen(port, () => {
  console.log(`Web SPA listening on port ${port}`);
});
