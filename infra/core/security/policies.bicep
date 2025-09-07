param resourceGroupName string
param location string = resourceGroup().location

// Define policy definitions for security guardrails
var policyDefinitions = [
  {
    name: 'require-https-function-apps'
    displayName: 'Function apps should only be accessible over HTTPS'
    description: 'Use of HTTPS ensures server/service authentication and protects data in transit from network layer eavesdropping attacks.'
    mode: 'Indexed'
    policyRule: {
      if: {
        allOf: [
          {
            field: 'type'
            equals: 'Microsoft.Web/sites'
          }
          {
            field: 'kind'
            contains: 'functionapp'
          }
          {
            field: 'Microsoft.Web/sites/httpsOnly'
            notEquals: true
          }
        ]
      }
      then: {
        effect: 'deny'
      }
    }
  }
  {
    name: 'require-storage-advanced-threat-protection'
    displayName: 'Advanced threat protection should be enabled on storage accounts'
    description: 'Advanced threat protection for storage accounts provides an additional layer of security intelligence that detects unusual and potentially harmful attempts to access or exploit storage accounts.'
    mode: 'Indexed'
    policyRule: {
      if: {
        field: 'type'
        equals: 'Microsoft.Storage/storageAccounts'
      }
      then: {
        effect: 'auditIfNotExists'
        details: {
          type: 'Microsoft.Security/advancedThreatProtectionSettings'
          existenceCondition: {
            field: 'Microsoft.Security/advancedThreatProtectionSettings/isEnabled'
            equals: true
          }
        }
      }
    }
  }
  {
    name: 'require-diagnostic-settings'
    displayName: 'Diagnostic settings should be configured for all resources'
    description: 'Diagnostic settings should be configured to forward platform logs and metrics to one or more destinations.'
    mode: 'Indexed'
    policyRule: {
      if: {
        anyOf: [
          {
            field: 'type'
            equals: 'Microsoft.Web/sites'
          }
          {
            field: 'type'
            equals: 'Microsoft.Storage/storageAccounts'
          }
          {
            field: 'type'
            equals: 'Microsoft.Insights/components'
          }
        ]
      }
      then: {
        effect: 'auditIfNotExists'
        details: {
          type: 'Microsoft.Insights/diagnosticSettings'
          existenceCondition: {
            allOf: [
              {
                count: {
                  field: 'Microsoft.Insights/diagnosticSettings/logs[*]'
                  where: {
                    field: 'Microsoft.Insights/diagnosticSettings/logs[*].enabled'
                    equals: true
                  }
                }
                greater: 0
              }
              {
                count: {
                  field: 'Microsoft.Insights/diagnosticSettings/metrics[*]'
                  where: {
                    field: 'Microsoft.Insights/diagnosticSettings/metrics[*].enabled'
                    equals: true
                  }
                }
                greater: 0
              }
            ]
          }
        }
      }
    }
  }
]

// Deploy policy definitions
resource policyDef 'Microsoft.Authorization/policyDefinitions@2021-06-01' = [for policy in policyDefinitions: {
  name: policy.name
  properties: {
    displayName: policy.displayName
    description: policy.description
    mode: policy.mode
    policyRule: policy.policyRule
  }
}]

// Create a policy initiative (policy set) combining all security policies
resource securityInitiative 'Microsoft.Authorization/policySetDefinitions@2021-06-01' = {
  name: 'emergency-management-security-initiative'
  properties: {
    displayName: 'Emergency Management Security Initiative'
    description: 'Security policies for Emergency Management Azure Functions application'
    policyDefinitions: [for (policy, i) in policyDefinitions: {
      policyDefinitionId: policyDef[i].id
      parameters: {}
    }]
  }
}

// Assign the policy initiative to the resource group
resource policyAssignment 'Microsoft.Authorization/policyAssignments@2022-06-01' = {
  name: 'emergency-management-security-assignment'
  location: location
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    displayName: 'Emergency Management Security Assignment'
    description: 'Assignment of security policies for Emergency Management application'
    policyDefinitionId: securityInitiative.id
    enforcementMode: 'Default'
    parameters: {}
  }
}

output policyAssignmentId string = policyAssignment.id
output policyInitiativeId string = securityInitiative.id