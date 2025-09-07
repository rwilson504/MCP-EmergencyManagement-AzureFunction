param principalID string
param roleDefinitionID string
param mapsAccountName string

resource mapsAccount 'Microsoft.Maps/accounts@2023-06-01' existing = {
  name: mapsAccountName
}

// Allow access from API to Azure Maps using a managed identity and least priv role
resource mapsRoleAssignment 'Microsoft.Authorization/roleAssignments@2020-04-01-preview' = {
  name: guid(mapsAccount.id, principalID, roleDefinitionID)
  scope: mapsAccount
  properties: {
    roleDefinitionId: resourceId('Microsoft.Authorization/roleDefinitions', roleDefinitionID)
    principalId: principalID
    principalType: 'ServicePrincipal' // Workaround for https://learn.microsoft.com/en-us/azure/role-based-access-control/role-assignments-template#new-service-principal
  }
}

output ROLE_ASSIGNMENT_NAME string = mapsRoleAssignment.name