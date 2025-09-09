import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// https://vitejs.dev/config/
export default defineConfig({
  plugins: [react()],
  build: {
    outDir: 'dist'
  },
  server: {
    port: 3000
  },
  publicDir: 'public',
  // Ensure web.config is copied to the output directory for Azure App Service
  assetsInclude: ['**/*.config']
})