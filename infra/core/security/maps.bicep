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
    // Allow key usage for demo purposes
    disableLocalAuth: false
  }
  tags: tags
}

output id string = maps.id
output name string = maps.name
output primaryKey string = listKeys(maps.id, '2023-06-01').primaryKey