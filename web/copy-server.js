// Copy server.js into dist for Zip Deploy start command fallback (if needed)
import fs from 'fs';
import path from 'path';

const src = path.join(process.cwd(), 'server.js');
const dest = path.join(process.cwd(), 'dist', 'server.js');
if (fs.existsSync(src)) {
  fs.copyFileSync(src, dest);
  console.log('Copied server.js to dist for deployment');
} else {
  console.warn('server.js not found to copy');
}
