param name string
param location string = resourceGroup().location
param sku string = 'G2'
param tags object = {}

resource maps 'Microsoft.Maps/accounts@2023-06-01' = {
  name: name
  location: location
  sku: {
    name: sku
  }
  properties: {
    // Disable local authentication to enforce Azure AD authentication only
    disableLocalAuth: true
  }
  tags: tags
}

output id string = maps.id
output name string = maps.name
output clientId string = maps.properties.uniqueId