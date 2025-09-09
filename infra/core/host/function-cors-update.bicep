@description('Function App name')
param functionAppName string

@description('Web App URL to add to CORS')
param webAppUrl string

resource functionApp 'Microsoft.Web/sites@2023-12-01' existing = {
  name: functionAppName
}

resource corsConfig 'Microsoft.Web/sites/config@2023-12-01' = {
  parent: functionApp
  name: 'web'
  properties: {
    cors: {
      allowedOrigins: [
        'http://localhost:3000'
        'https://localhost:3000'
        webAppUrl
      ]
      supportCredentials: true
    }
    use32BitWorkerProcess: false
    ftpsState: 'Disabled'
  }
}