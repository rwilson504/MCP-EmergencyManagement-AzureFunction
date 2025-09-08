targetScope = 'subscription'

@description('Location for the policy assignments')
param location string = deployment().location

@description('Tags to apply to the policy assignments')
param tags object = {}

// HTTPS enforcement policy for Function Apps
resource httpsEnforcementPolicy 'Microsoft.Authorization/policyDefinitions@2021-06-01' = {
  name: 'emergency-mgmt-https-enforcement'
  properties: {
    displayName: 'Emergency Management - Require HTTPS for Function Apps'
    description: 'Ensures all Function Apps in the Emergency Management system use HTTPS only'
    policyType: 'Custom'
    mode: 'All'
    parameters: {}
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
}

// Diagnostic settings policy for Function Apps
resource diagnosticsRequiredPolicy 'Microsoft.Authorization/policyDefinitions@2021-06-01' = {
  name: 'emergency-mgmt-diagnostics-required'
  properties: {
    displayName: 'Emergency Management - Require Diagnostic Settings'
    description: 'Ensures all Emergency Management resources have diagnostic settings configured'
    policyType: 'Custom'
    mode: 'All'
    parameters: {
      logAnalyticsWorkspaceId: {
        type: 'String'
        metadata: {
          displayName: 'Log Analytics Workspace ID'
          description: 'The ID of the Log Analytics workspace to send diagnostic logs to'
        }
      }
    }
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
        effect: 'deployIfNotExists'
        details: {
          type: 'Microsoft.Insights/diagnosticSettings'
          roleDefinitionIds: [
            '/providers/Microsoft.Authorization/roleDefinitions/92aaf0da-9dab-42b6-94a3-d43ce8d16293' // Log Analytics Contributor
          ]
          deployment: {
            properties: {
              mode: 'incremental'
              template: {
                '$schema': 'https://schema.management.azure.com/schemas/2019-04-01/deploymentTemplate.json#'
                contentVersion: '1.0.0.0'
                parameters: {
                  resourceName: {
                    type: 'string'
                  }
                  logAnalyticsWorkspaceId: {
                    type: 'string'
                  }
                  resourceType: {
                    type: 'string'
                  }
                }
                resources: [
                  {
                    type: 'Microsoft.Insights/diagnosticSettings'
                    apiVersion: '2021-05-01-preview'
                    name: 'security-diagnostics'
                    scope: '[parameters(\'resourceName\')]'
                    properties: {
                      workspaceId: '[parameters(\'logAnalyticsWorkspaceId\')]'
                      metrics: [
                        {
                          category: 'AllMetrics'
                          enabled: true
                          retentionPolicy: {
                            enabled: true
                            days: 90
                          }
                        }
                      ]
                      logs: [
                        {
                          categoryGroup: 'allLogs'
                          enabled: true
                          retentionPolicy: {
                            enabled: true
                            days: 90
                          }
                        }
                      ]
                    }
                  }
                ]
              }
              parameters: {
                resourceName: {
                  value: '[field(\'id\')]'
                }
                logAnalyticsWorkspaceId: {
                  value: '[parameters(\'logAnalyticsWorkspaceId\')]'
                }
                resourceType: {
                  value: '[field(\'type\')]'
                }
              }
            }
          }
        }
      }
    }
  }
}

// Storage advanced threat protection policy
resource storageAdvancedThreatProtectionPolicy 'Microsoft.Authorization/policyDefinitions@2021-06-01' = {
  name: 'emergency-mgmt-storage-threat-protection'
  properties: {
    displayName: 'Emergency Management - Storage Advanced Threat Protection'
    description: 'Ensures storage accounts have advanced threat protection enabled'
    policyType: 'Custom'
    mode: 'All'
    parameters: {}
    policyRule: {
      if: {
        field: 'type'
        equals: 'Microsoft.Storage/storageAccounts'
      }
      then: {
        effect: 'deployIfNotExists'
        details: {
          type: 'Microsoft.Security/advancedThreatProtectionSettings'
          roleDefinitionIds: [
            '/providers/Microsoft.Authorization/roleDefinitions/fb1c8493-542b-48eb-b624-b4c8fea62acd' // Security Admin
          ]
          deployment: {
            properties: {
              mode: 'incremental'
              template: {
                '$schema': 'https://schema.management.azure.com/schemas/2019-04-01/deploymentTemplate.json#'
                contentVersion: '1.0.0.0'
                parameters: {
                  storageAccountName: {
                    type: 'string'
                  }
                }
                resources: [
                  {
                    type: 'Microsoft.Storage/storageAccounts/providers/advancedThreatProtectionSettings'
                    apiVersion: '2019-01-01'
                    name: '[concat(parameters(\'storageAccountName\'), \'/Microsoft.Security/current\')]'
                    properties: {
                      isEnabled: true
                    }
                  }
                ]
              }
              parameters: {
                storageAccountName: {
                  value: '[field(\'name\')]'
                }
              }
            }
          }
        }
      }
    }
  }
}

output httpsEnforcementPolicyId string = httpsEnforcementPolicy.id
output diagnosticsRequiredPolicyId string = diagnosticsRequiredPolicy.id
output storageAdvancedThreatProtectionPolicyId string = storageAdvancedThreatProtectionPolicy.id