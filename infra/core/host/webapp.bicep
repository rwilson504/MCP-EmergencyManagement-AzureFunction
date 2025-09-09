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

resource webApp 'Microsoft.Web/sites@2023-12-01' = {
  name: name
  location: location
  tags: union(tags, { 'azd-service-name': serviceName })
  kind: 'app,linux'
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
      // Configure for serving static React SPA files
      appSettings: [
        {
          name: 'WEBSITE_NODE_DEFAULT_VERSION'
          // Pin the exact Node.js version used by the build toolchain at deploy time
          value: nodeVersion
        }
        {
          name: 'APP_NODE_VERSION'
          // Exposed for diagnostics & frontend to display expected runtime
          value: nodeVersion
        }
        {
          name: 'SCM_DO_BUILD_DURING_DEPLOYMENT'
          value: 'false' // We pre-build the React app
        }
        {
          name: 'WEBSITE_RUN_FROM_PACKAGE'
          value: '1'
        }
      ]
      // Configure default documents
      defaultDocuments: [
        'index.html'
      ]
    }
    httpsOnly: true
    clientAffinityEnabled: false
  }
}

// Output values
output webAppName string = webApp.name
output webAppId string = webApp.id
output defaultHostname string = webApp.properties.defaultHostName
output webAppUrl string = 'https://${webApp.properties.defaultHostName}'
