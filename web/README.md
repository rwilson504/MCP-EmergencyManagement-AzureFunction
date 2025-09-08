# Azure Maps Viewer

This directory contains a React TypeScript single-page application (SPA) for visualizing emergency management routes produced by the MCP tools.

## Features

- **Secure Azure Maps Integration**: Uses anonymous authentication with server-side token broker
- **Route Visualization**: Displays fire-aware routes with avoided areas highlighted
- **Short-Link Support**: Shareable URLs for route specifications
- **Real-time Updates**: Fetches latest route data from Azure Functions API

## Development

1. **Install Dependencies**:
   ```bash
   npm install
   ```

2. **Environment Setup**:
   Copy `.env.example` to `.env` and configure:
   ```
   VITE_AZURE_MAPS_CLIENT_ID=your-azure-maps-client-id
   ```

3. **Start Development Server**:
   ```bash
   npm run dev
   ```

4. **Build for Production**:
   ```bash
   npm run build
   ```

## Deployment

The built application (in `dist/`) can be served from:
- Azure Static Web Apps
- Azure App Service
- Any static hosting service with Easy Auth or equivalent

The key requirement is that the hosting solution:
1. Handles user authentication
2. Can proxy API calls to the Azure Functions backend
3. Supports CORS for Azure Maps REST API calls

## API Dependencies

The SPA requires these Function App endpoints:
- `GET /api/maps-token` - Azure Maps token broker
- `GET /api/routeLinks/{id}` - Route specification retrieval
- `POST /api/routeLinks` - Route link creation (used by MCP agents)

## Browser Requirements

- Modern browser with ES2020+ support
- WebGL support for Azure Maps rendering
- Fetch API and Promises support