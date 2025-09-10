# Web App Container Fix and Enhanced Logging

This document outlines the fixes applied to resolve the web app container startup issue and the enhanced logging configuration that was added.

## Issues Fixed

### 1. Container Startup Failure
**Problem**: Web app container was failing with error:
```
Error [ERR_MODULE_NOT_FOUND]: Cannot find package 'express' imported from /home/site/wwwroot/server.js
```

**Root Cause**: Build automation (Oryx) was not properly configured during Azure deployment, resulting in missing npm dependencies.

**Solution**: Updated `infra/core/host/webapp.bicep` with proper Oryx build configuration:
- Added `ENABLE_ORYX_BUILD: 'true'` 
- Added `BUILD_FLAGS: 'UseAppServiceBuild=true'`
- Added `PROJECT: 'web'` to specify the correct project path
- Added `appCommandLine: 'npm start'` for proper startup
- Updated build script in `web/package.json` to include server.js copying

### 2. Enhanced Container Logging
**Added comprehensive logging configuration**:
- Created new `infra/core/monitor/container-logs.bicep` module
- Enhanced diagnostic settings with additional log categories:
  - AppServiceIPSecAuditLogs
  - AppServiceAuditLogs
  - Console, HTTP, App, and Platform logs
- Configured container-specific logging settings
- Integrated with Log Analytics workspace

## Testing the Fixes

### 1. Deploy the Updated Infrastructure
```bash
# Deploy with AZD
azd up

# Or provision only infrastructure
azd provision
```

### 2. Verify Container Logging
After deployment, you can check container logs using:

```bash
# Enable container logging (if not already enabled)
az webapp log config --name <web-app-name> --resource-group <resource-group-name> --docker-container-logging filesystem

# Stream container logs
az webapp log tail --name <web-app-name> --resource-group <resource-group-name>
```

### 3. Check Build Process
Monitor the deployment logs to verify:
- Oryx build runs successfully
- npm install completes without errors
- Dependencies are properly installed
- Build artifacts are created correctly

### 4. Verify Web App Functionality
1. Navigate to the web app URL
2. Check that the Emergency Management Maps Viewer loads
3. Verify Azure Maps integration works
4. Check browser console for any JavaScript errors

## Build Process Validation

The build process now correctly:
1. Runs `npm install` during deployment (via Oryx)
2. Executes `npm run build` which includes:
   - TypeScript compilation (`tsc`)
   - Vite build for React SPA
   - Server.js copying to dist folder
3. Starts the Express server with `npm start`

## Log Analytics Integration

Enhanced logging sends logs to Log Analytics workspace for:
- Container startup diagnostics
- Application errors and warnings
- HTTP request logs
- Platform and audit logs
- Security audit logs

Query examples for Log Analytics:
```kusto
// Container startup logs
AppServiceConsoleLogs_CL
| where TimeGenerated > ago(1h)
| where ResultDescription contains "startup"

// Application errors
AppServiceAppLogs_CL  
| where TimeGenerated > ago(1h)
| where Level == "Error"

// HTTP request patterns
AppServiceHTTPLogs_CL
| where TimeGenerated > ago(1h)
| summarize RequestCount = count() by Method, UriStem
```

## Configuration Summary

### Key App Settings Added:
- `ENABLE_ORYX_BUILD=true`
- `BUILD_FLAGS=UseAppServiceBuild=true` 
- `PROJECT=web`
- `SCM_DO_BUILD_DURING_DEPLOYMENT=true`

### Startup Configuration:
- `appCommandLine: npm start`
- `linuxFxVersion: NODE|22-lts`

### Logging Configuration:
- File system logging enabled
- HTTP logs with 7-day retention
- Verbose logging level
- Integration with Log Analytics workspace

## Next Steps

1. Deploy the changes using `azd up`
2. Monitor the deployment logs for successful Oryx build
3. Verify the web app starts without container errors
4. Test the maps functionality
5. Review logs in Azure portal and Log Analytics