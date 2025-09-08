/// <reference types="vite/client" />

interface ImportMetaEnv {
  readonly VITE_AZURE_MAPS_CLIENT_ID: string
}

interface ImportMeta {
  readonly env: ImportMetaEnv
}