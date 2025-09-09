@description('Name of the Web App')
param name string

@description('Location for the Web App')
param location string

@description('App Service Plan ID')
param appServicePlanId string

@description('Tags to apply to the Web App')
param tags object = {}

@description('Runtime stack for the web app')
param linuxFxVersion string = 'NODE|18-lts'

resource webApp 'Microsoft.Web/sites@2023-12-01' = {
  name: name
  location: location
  tags: tags
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
          value: '18.19.0'
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