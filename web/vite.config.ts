import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// https://vitejs.dev/config/
// Build version injected for telemetry correlation (AI + source maps). Prefer CI-provided values.
const buildVersion = process.env.BUILD_VERSION || process.env.GITHUB_SHA || new Date().toISOString();

export default defineConfig({
  plugins: [react()],
  build: {
    outDir: 'dist',
    // Generate source maps so stack traces can be symbolicated in Application Insights
    sourcemap: true
  },
  define: {
    __APP_BUILD_VERSION__: JSON.stringify(buildVersion)
  },
  server: {
    port: 3000
  },
  publicDir: 'public',
  // Ensure web.config is copied to the output directory for Azure App Service
  assetsInclude: ['**/*.config']
})