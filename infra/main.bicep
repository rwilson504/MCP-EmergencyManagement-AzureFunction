targetScope = 'subscription'

@minLength(1)
@maxLength(64)
@description('Name of the the environment which is used to generate a short unique hash used in all resources.')
param environmentName string

@minLength(1)
@description('Primary location for all resources')
@allowed(['australiaeast', 'eastasia', 'eastus', 'eastus2', 'northeurope', 'southcentralus', 'southeastasia', 'swedencentral', 'uksouth', 'westus2', 'eastus2euap'])
@metadata({
  azd: {
    type: 'location'
  }
})
param location string
param vnetEnabled bool
param apiServiceName string = ''
param apiUserAssignedIdentityName string = ''
param applicationInsightsName string = ''
param appServicePlanName string = ''
@description('Optional dedicated App Service Plan name for the web frontend (if empty a name will be generated).')
param webAppServicePlanName string = ''
param logAnalyticsName string = ''
param resourceGroupName string = ''
param storageAccountName string = ''
param vNetName string = ''
param disableLocalAuth bool = true

// Fire-Aware Routing Parameters
@description('Azure Maps account name (must be globally unique in subscription)')
param mapsAccountName string = ''

@description('Azure Maps SKU')
@allowed(['G2'])
param mapsSku string = 'G2'

@description('Geo cache container name')
param geoCacheContainerName string = 'routing-cache'

// Web App Parameters
@description('Web App name (must be globally unique)')
param webAppName string = ''

@description('Runtime stack for the web app (Node LTS)')
param linuxFxVersion string = 'NODE|22-lts'

var abbrs = loadJsonContent('./abbreviations.json')
var resourceToken = toLower(uniqueString(subscription().id, environmentName, location))
var tags = { 'azd-env-name': environmentName }
var functionAppName = !empty(apiServiceName) ? apiServiceName : 'doem-${abbrs.webSitesFunctions}api-${resourceToken}'
var deploymentStorageContainerName = 'app-package-${take(functionAppName, 32)}-${take(toLower(uniqueString(functionAppName, resourceToken)), 7)}'

// Web App name with fallback
var finalWebAppName = !empty(webAppName) ? webAppName : 'doem-${abbrs.webSitesAppService}${resourceToken}'
// NOTE: Removed precomputed subnet variables that referenced conditional module outputs to avoid Bicep null access diagnostics.
// We'll inline conditional dereferencing after the module declaration.

// Organize resources in a resource group
resource rg 'Microsoft.Resources/resourceGroups@2021-04-01' = {
  name: !empty(resourceGroupName) ? resourceGroupName : 'doem-${abbrs.resourcesResourceGroups}${environmentName}'
  location: location
  tags: tags
}

// User assigned managed identity to be used by the function app to reach storage and service bus
module apiUserAssignedIdentity './core/identity/userAssignedIdentity.bicep' = {
  name: 'apiUserAssignedIdentity'
  scope: rg
  params: {
    location: location
    tags: tags
    identityName: !empty(apiUserAssignedIdentityName) ? apiUserAssignedIdentityName : 'doem-${abbrs.managedIdentityUserAssignedIdentities}api-${resourceToken}'
  }
}

// The application backend is a function app
module appServicePlan './core/host/appserviceplan.bicep' = {
  name: 'appserviceplan'
  scope: rg
  params: {
    name: !empty(appServicePlanName) ? appServicePlanName : 'doem-${abbrs.webServerFarms}${resourceToken}'
    location: location
    tags: tags
    sku: {
      name: 'FC1'
      tier: 'FlexConsumption'
    }
  }
}

// Dedicated multi-tenant App Service Plan for the static React web app (cannot share Flex Consumption plan used by Functions)
module webAppServicePlan './core/host/appserviceplan.bicep' = {
  name: 'webappserviceplan'
  scope: rg
  params: {
    name: !empty(webAppServicePlanName) ? webAppServicePlanName : 'doem-${abbrs.webServerFarms}web-${resourceToken}'
    location: location
    tags: tags
    // Use Basic B1 (Linux) because Free (F1) is not supported for Linux plans in this scenario
    sku: {
      name: 'B1'
      tier: 'Basic'
    }
    kind: 'linux'
  }
}

// Virtual Network & private endpoint to blob storage (define before resources that may consume its outputs)
module serviceVirtualNetwork 'app/vnet.bicep' =  if (vnetEnabled) {
  name: 'serviceVirtualNetwork'
  scope: rg
  params: {
    location: location
    tags: tags
    vNetName: !empty(vNetName) ? vNetName : 'doem-${abbrs.networkVirtualNetworks}${resourceToken}'
  }
}
// Deterministic subnet identifiers (avoid referencing conditional module outputs directly to satisfy analyzer)
var vNetResolvedName = !empty(vNetName) ? vNetName : 'doem-${abbrs.networkVirtualNetworks}${resourceToken}'
@description('App subnet resource ID if VNet integration is enabled, else empty string.')
var safeAppSubnetId = vnetEnabled ? '/subscriptions/${subscription().subscriptionId}/resourceGroups/${rg.name}/providers/Microsoft.Network/virtualNetworks/${vNetResolvedName}/subnets/app' : ''
@description('Private endpoint subnet name when VNet enabled.')
var safePrivateEndpointSubnetName = vnetEnabled ? 'private-endpoints-subnet' : ''

// Azure Maps account for fire-aware routing
var finalMapsAccountName = !empty(mapsAccountName) ? mapsAccountName : 'doem-maps-${resourceToken}'

module maps 'core/security/maps.bicep' = {
  name: 'maps'
  scope: rg
  params: {
    name: finalMapsAccountName
    location: location
    sku: mapsSku
    tags: tags
  }
}

module api './app/api.bicep' = {
  name: 'api'
  scope: rg
  params: {
    name: functionAppName
    location: location
    tags: tags
    applicationInsightsName: monitoring.outputs.applicationInsightsName
    appServicePlanId: appServicePlan.outputs.id
    runtimeName: 'dotnet-isolated'
    runtimeVersion: '8.0'
    storageAccountName: storage.outputs.name
    deploymentStorageContainerName: deploymentStorageContainerName
    identityId: apiUserAssignedIdentity.outputs.identityId
    identityClientId: apiUserAssignedIdentity.outputs.identityClientId
    // Deterministic CORS origin (Pattern A) avoids referencing web module outputs, preventing circular dependency
    additionalCorsOrigins: [ 'https://${(!empty(webAppName) ? webAppName : finalWebAppName)}.azurewebsites.net' ]
    appSettings: {
      // Use environment() for storage suffix to avoid hardcoding public cloud DNS
      Storage__BlobServiceUrl: 'https://${storage.outputs.name}.blob.${environment().suffixes.storage}'
      Storage__CacheContainer: geoCacheContainerName
      Fires__ArcGisFeatureUrl: 'https://services3.arcgis.com/T4QMspbfLg3qTGWY/ArcGIS/rest/services/WFIGS_Interagency_Perimeters_YearToDate/FeatureServer/0/query'
      Maps__RouteBase: 'https://atlas.microsoft.com'
      Maps__SearchBase: 'https://atlas.microsoft.com'
      Maps__ClientId: maps.outputs.clientId
      ManagedIdentity__ClientId: apiUserAssignedIdentity.outputs.identityClientId
      // Base URL for route link generation - points to the web app instead of function host
      RouteLinks__BaseUrl: 'https://${(!empty(webAppName) ? webAppName : finalWebAppName)}.azurewebsites.net'
    }
    // Only provide subnet ID if VNet is enabled (prevents null-module output dereference)
  virtualNetworkSubnetId: safeAppSubnetId
  }
}

// Web App for hosting React frontend
module webApp 'core/host/webapp.bicep' = {
  name: 'webapp'
  scope: rg
  params: {
    name: finalWebAppName
    location: location
    // Use dedicated web plan (Free tier) because FlexConsumption (FC1) cannot host a standard Web App
    appServicePlanId: webAppServicePlan.outputs.id
    tags: tags
    linuxFxVersion: linuxFxVersion
    serviceName: 'web'
    applicationInsightsConnectionString: monitoring.outputs.applicationInsightsConnectionString
    logAnalyticsWorkspaceId: monitoring.outputs.logAnalyticsWorkspaceId
    userAssignedIdentityId: apiUserAssignedIdentity.outputs.identityId
    userAssignedIdentityClientId: apiUserAssignedIdentity.outputs.identityClientId
    apiBaseUrl: 'https://${functionAppName}.azurewebsites.net/api'
  }
}

// (CORS applied via api module's additionalCorsOrigins param above; no separate config resource needed)

// Backing storage for Azure functions api
module storage './core/storage/storage-account.bicep' = {
  name: 'storage'
  scope: rg
  params: {
    name: !empty(storageAccountName) ? storageAccountName : 'doem${abbrs.storageStorageAccounts}${resourceToken}'
    location: location
    tags: tags
    containers: [{name: deploymentStorageContainerName}, {name: 'snippets'}, {name: geoCacheContainerName}, {name: 'links'}]
    publicNetworkAccess: vnetEnabled ? 'Disabled' : 'Enabled'
    networkAcls: !vnetEnabled ? {} : {
      defaultAction: 'Deny'
    }
  }
}

var StorageBlobDataOwner = 'b7e6dc6d-f1e8-4753-8033-0f276bb0955b'
var StorageQueueDataContributor = '974c5e8b-45b9-4653-ba55-5f855dd0fb88'

// Allow access from api to blob storage using a managed identity
module blobRoleAssignmentApi 'app/storage-Access.bicep' = {
  name: 'blobRoleAssignmentapi'
  scope: rg
  params: {
    storageAccountName: storage.outputs.name
    roleDefinitionID: StorageBlobDataOwner
    principalID: apiUserAssignedIdentity.outputs.identityPrincipalId
  }
}

// Allow access from api to queue storage using a managed identity
module queueRoleAssignmentApi 'app/storage-Access.bicep' = {
  name: 'queueRoleAssignmentapi'
  scope: rg
  params: {
    storageAccountName: storage.outputs.name
    roleDefinitionID: StorageQueueDataContributor
    principalID: apiUserAssignedIdentity.outputs.identityPrincipalId
  }
}

module storagePrivateEndpoint 'app/storage-PrivateEndpoint.bicep' = if (vnetEnabled) {
  name: 'servicePrivateEndpoint'
  scope: rg
  params: {
    location: location
    tags: tags
    virtualNetworkName: !empty(vNetName) ? vNetName : 'doem-${abbrs.networkVirtualNetworks}${resourceToken}'
    // Safe because this module only exists when vnetEnabled is true
  subnetName: safePrivateEndpointSubnetName
    resourceName: storage.outputs.name
  }
}

// Monitor application with Azure Monitor
module monitoring './core/monitor/monitoring.bicep' = {
  name: 'monitoring'
  scope: rg
  params: {
    location: location
    tags: tags
    logAnalyticsName: !empty(logAnalyticsName) ? logAnalyticsName : 'doem-${abbrs.operationalInsightsWorkspaces}${resourceToken}'
    applicationInsightsName: !empty(applicationInsightsName) ? applicationInsightsName : 'doem-${abbrs.insightsComponents}${resourceToken}'
    disableLocalAuth: disableLocalAuth  
  }
}

var monitoringRoleDefinitionId = '3913510d-42f4-4e42-8a64-420c390055eb' // Monitoring Metrics Publisher role ID
var mapsDataReaderRoleDefinitionId = '423170ca-a8f6-4b0f-8487-9e4eb8f49bfa' // Azure Maps Data Reader role ID

// Allow access from api to application insights using a managed identity
module appInsightsRoleAssignmentApi './core/monitor/appinsights-access.bicep' = {
  name: 'appInsightsRoleAssignmentapi'
  scope: rg
  params: {
    appInsightsName: monitoring.outputs.applicationInsightsName
    roleDefinitionID: monitoringRoleDefinitionId
    principalID: apiUserAssignedIdentity.outputs.identityPrincipalId
  }
}

// Allow access from api to Azure Maps using a managed identity
module mapsRoleAssignmentApi './core/security/maps-access.bicep' = {
  name: 'mapsRoleAssignmentapi'
  scope: rg
  params: {
    mapsAccountName: maps.outputs.name
    roleDefinitionID: mapsDataReaderRoleDefinitionId
    principalID: apiUserAssignedIdentity.outputs.identityPrincipalId
  }
}

// App outputs
output APPLICATIONINSIGHTS_CONNECTION_STRING string = monitoring.outputs.applicationInsightsConnectionString
output AZURE_LOCATION string = location
output AZURE_TENANT_ID string = tenant().tenantId
output SERVICE_API_NAME string = api.outputs.SERVICE_API_NAME
output AZURE_FUNCTION_NAME string = api.outputs.SERVICE_API_NAME
output AZURE_MAPS_ACCOUNT_NAME string = maps.outputs.name
output GEO_CACHE_CONTAINER_URI string = 'https://${storage.outputs.name}.blob.${environment().suffixes.storage}/${geoCacheContainerName}'
output WEB_APP_NAME string = webApp.outputs.webAppName
output WEB_APP_URL string = webApp.outputs.webAppUrl
// Standard azd discovery outputs for the 'web' service (needed for azd to deploy static artifacts)
output SERVICE_WEB_NAME string = webApp.outputs.webAppName
output SERVICE_WEB_URL string = webApp.outputs.webAppUrl
output SERVICE_WEB_HOSTNAME string = replace(webApp.outputs.webAppUrl, 'https://', '')
