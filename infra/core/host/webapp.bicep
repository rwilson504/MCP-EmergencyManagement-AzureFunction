@description('Name of the Web App')
param name string

@description('Location for the Web App')
param location string

@description('App Service Plan ID')
param appServicePlanId string

@description('Tags to apply to the Web App')
param tags object = {}

@description('Runtime stack for the web app')
param linuxFxVersion string = 'NODE|22-lts'

@description('Node.js version used for App Service environment (WEBSITE_NODE_DEFAULT_VERSION / APP_NODE_VERSION).')
param nodeVersion string = '22.11.0'

@description('Service name for AZD tagging')
param serviceName string = 'web'

@description('Optional Application Insights connection string to enable client/server telemetry correlation.')
param applicationInsightsConnectionString string = ''

@description('Optional Log Analytics Workspace Resource ID to send platform & application logs.')
param logAnalyticsWorkspaceId string = ''

@description('Optional user-assigned managed identity resource ID to attach to the Web App.')
param userAssignedIdentityId string = ''

@description('Optional user-assigned managed identity client ID (exposed to runtime as AZURE_CLIENT_ID for DefaultAzureCredential).')
param userAssignedIdentityClientId string = ''

@description('Optional public API base URL (e.g., Function App endpoint) to expose to the SPA at runtime.')
param apiBaseUrl string = ''

@description('Optional Azure Maps Client ID for map rendering.')
param azureMapsClientId string = ''

@description('Revision marker to force app settings redeployment when incremented.')
param appSettingsRevision string = '2025-09-10.1'

// NOTE: Simpler explicit array (leaves empty AI connection string if not provided)

resource webApp 'Microsoft.Web/sites@2023-12-01' = {
  name: name
  location: location
  tags: union(tags, { 'azd-service-name': serviceName })
  kind: 'app,linux'
  identity: !empty(userAssignedIdentityId) ? {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${userAssignedIdentityId}': {}
    }
  } : null
  properties: {
    serverFarmId: appServicePlanId
    siteConfig: {
      linuxFxVersion: linuxFxVersion
      ftpsState: 'FtpsOnly'
      minTlsVersion: '1.2'
      scmMinTlsVersion: '1.2'
      use32BitWorkerProcess: false
      webSocketsEnabled: false
      managedPipelineMode: 'Integrated'
      // Specify startup command to ensure the web app starts with the correct server
      appCommandLine: 'npm start'
      // Configure default documents
      defaultDocuments: [
        'index.html'
      ]
    }
    httpsOnly: true
    clientAffinityEnabled: false
  }
}

// Dedicated child resource for Web App application settings to ensure reliable incremental updates.
resource webAppAppSettings 'Microsoft.Web/sites/config@2023-12-01' = {
  name: 'appsettings'
  parent: webApp
  properties: {
    WEBSITE_NODE_DEFAULT_VERSION: nodeVersion
    APP_NODE_VERSION: nodeVersion
    APPLICATIONINSIGHTS_CONNECTION_STRING: applicationInsightsConnectionString
    APPINSIGHTS_INSTRUMENTATIONKEY: empty(applicationInsightsConnectionString) ? '' : split(split(applicationInsightsConnectionString, ';')[0], '=')[1]
    SCM_DO_BUILD_DURING_DEPLOYMENT: 'true'
    ENABLE_ORYX_BUILD: 'true'
    BUILD_FLAGS: 'UseAppServiceBuild=true'
    PROJECT: 'web'
    API_BASE_URL: apiBaseUrl
    AZURE_CLIENT_ID: userAssignedIdentityClientId
    AZURE_MAPS_CLIENT_ID: azureMapsClientId
    APP_SETTINGS_REVISION: appSettingsRevision
  }
}

// Diagnostic settings to route Web App logs & metrics to Log Analytics (if workspace provided)
resource webAppDiagnostics 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = if (!empty(logAnalyticsWorkspaceId)) {
  name: 'webapp-logs'
  scope: webApp
  properties: {
    workspaceId: logAnalyticsWorkspaceId
    logs: [
      {
        category: 'AppServiceHTTPLogs'
        enabled: true
        retentionPolicy: {
          enabled: false
          days: 0
        }
      }
      {
        category: 'AppServiceConsoleLogs'
        enabled: true
        retentionPolicy: {
          enabled: false
          days: 0
        }
      }
      {
        category: 'AppServiceAppLogs'
        enabled: true
        retentionPolicy: {
          enabled: false
          days: 0
        }
      }
      {
        category: 'AppServicePlatformLogs'
        enabled: true
        retentionPolicy: {
          enabled: false
          days: 0
        }
      }
      {
        category: 'AppServiceIPSecAuditLogs'
        enabled: true
        retentionPolicy: {
          enabled: false
          days: 0
        }
      }
      {
        category: 'AppServiceAuditLogs'
        enabled: true
        retentionPolicy: {
          enabled: false
          days: 0
        }
      }
    ]
    metrics: [
      {
        category: 'AllMetrics'
        enabled: true
        retentionPolicy: {
          enabled: false
          days: 0
        }
      }
    ]
  }
}

// Output values
output webAppName string = webApp.name
output webAppId string = webApp.id
output defaultHostname string = webApp.properties.defaultHostName
output webAppUrl string = 'https://${webApp.properties.defaultHostName}'
