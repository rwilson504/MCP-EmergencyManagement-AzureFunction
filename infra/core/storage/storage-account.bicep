param name string
param location string = resourceGroup().location
param tags object = {}

param allowBlobPublicAccess bool = false
@allowed(['Enabled', 'Disabled'])
param publicNetworkAccess string = 'Enabled'
// containers parameter accepts objects: { name: 'containerName', publicAccess: 'None' }
// Ensure 'links' container (for route specs) is present; main template can merge if already provided.
param containers array = []
@description('If true, creates the links container (only if not already listed in containers param).')
param addLinksContainer bool = true
param kind string = 'StorageV2'
param minimumTlsVersion string = 'TLS1_2'
param sku object = { name: 'Standard_LRS' }
param networkAcls object = {
  bypass: 'AzureServices'
  defaultAction: 'Allow'
}

// Derive whether caller already supplied a 'links' container
var linkNames = [for c in containers: c.name]
var hasLinksContainer = contains(linkNames, 'links')

resource storage 'Microsoft.Storage/storageAccounts@2023-01-01' = {
  name: name
  location: location
  tags: tags
  kind: kind
  sku: sku
  properties: {
    minimumTlsVersion: minimumTlsVersion
    allowBlobPublicAccess: allowBlobPublicAccess
    publicNetworkAccess: publicNetworkAccess
    allowSharedKeyAccess: false
    networkAcls: networkAcls
  }

  resource blobServices 'blobServices' = {
    name: 'default'
    // Containers passed in by caller
    resource container 'containers' = [for container in containers: {
      name: container.name
      properties: {
        publicAccess: container.?publicAccess ?? 'None'
      }
    }]
    resource linksContainer 'containers' = if (addLinksContainer && !hasLinksContainer) {
      name: 'links'
      properties: {
        publicAccess: 'None'
      }
    }
  }
}

output name string = storage.name
output primaryEndpoints object = storage.properties.primaryEndpoints
