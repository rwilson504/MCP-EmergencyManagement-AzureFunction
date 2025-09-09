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
   VITE_API_BASE_URL=http://localhost:7071/api
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

The application is deployed as an Azure Static Web App through the Bicep infrastructure templates. The deployment process:

1. **Infrastructure Provisioning**:
   ```bash
   azd provision
   ```
   This creates:
   - Azure Static Web App for hosting the React SPA
   - Azure Functions for the API backend  
   - Integration between Static Web App and Functions

2. **Application Deployment**:
   ```bash
   azd deploy
   ```
   This deploys both the React frontend and Azure Functions backend.

3. **Accessing the Application**:
   After deployment, the Static Web App URL will be available in the Azure portal or via:
   ```bash
   azd show
   ```

### Manual Deployment Options

If deploying outside of AZD, the built application (in `dist/`) can be served from:
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