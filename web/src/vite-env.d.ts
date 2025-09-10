/// <reference types="vite/client" />

interface ImportMetaEnv {
  readonly VITE_AZURE_MAPS_CLIENT_ID: string
  readonly VITE_API_BASE_URL: string
}

interface ImportMeta {
  readonly env: ImportMetaEnv
}

// Runtime-injected global variables
declare global {
  interface Window {
    __APPINSIGHTS_INSTRUMENTATION_KEY__?: string;
    __APPINSIGHTS_CONNECTION_STRING__?: string;
    __API_BASE_URL__?: string;
    __AZURE_MAPS_CLIENT_ID__?: string;
  }
}