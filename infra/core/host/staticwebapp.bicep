@description('Name of the Static Web App')
param name string

@description('Location for the Static Web App')
param location string = 'centralus'

@description('SKU for the Static Web App')
@allowed(['Free', 'Standard'])
param sku string = 'Free'

@description('Tags to apply to the Static Web App')
param tags object = {}

@description('Function App resource ID for API backend integration')
param functionAppResourceId string = ''

@description('Custom domain for the Static Web App (optional)')
param customDomain string = ''

resource staticWebApp 'Microsoft.Web/staticSites@2023-01-01' = {
  name: name
  location: location
  tags: tags
  sku: {
    name: sku
    tier: sku
  }
  properties: {
    // Build configuration for React SPA
    buildProperties: {
      appLocation: '/web'
      outputLocation: 'dist'
      appBuildCommand: 'npm run build'
      apiBuildCommand: ''
      skipGithubActionWorkflowGeneration: true
    }

    // Enable authentication providers
    provider: 'Custom'
    
    // Enable staging environments
    stagingEnvironmentPolicy: 'Enabled'
  }
}

// Configure custom domain if provided
resource customDomainResource 'Microsoft.Web/staticSites/customDomains@2023-01-01' = if (!empty(customDomain)) {
  parent: staticWebApp
  name: customDomain
  properties: {}
}

// Configure Function App backend integration
resource functionIntegration 'Microsoft.Web/staticSites/linkedBackends@2023-01-01' = if (!empty(functionAppResourceId)) {
  parent: staticWebApp
  name: 'functionBackend'
  properties: {
    backendResourceId: functionAppResourceId
    region: location
  }
}

// Output values
output staticWebAppName string = staticWebApp.name
output staticWebAppId string = staticWebApp.id
output defaultHostname string = staticWebApp.properties.defaultHostname
output repositoryUrl string = staticWebApp.properties.repositoryUrl