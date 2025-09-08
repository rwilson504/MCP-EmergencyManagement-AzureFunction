@description('The name of the resource to apply diagnostics to')
param resourceName string

@description('The resource ID of the Log Analytics workspace')
param logAnalyticsWorkspaceId string

@description('The type of resource to configure diagnostics for')
@allowed(['StorageAccount', 'FunctionApp', 'ApplicationInsights', 'VirtualNetwork'])
param resourceType string

@description('Diagnostic setting name')
param diagnosticSettingName string = 'security-diagnostics'

// Storage Account diagnostic settings
resource storageAccount 'Microsoft.Storage/storageAccounts@2023-01-01' existing = {
  name: resourceName
}

resource storageAccountDiagnostics 'Microsoft.Insights/diagnosticsettings@2021-05-01-preview' = if (resourceType == 'StorageAccount') {
  name: diagnosticSettingName
  scope: storageAccount
  properties: {
    workspaceId: logAnalyticsWorkspaceId
    metrics: [
      {
        category: 'Transaction'
        enabled: true
        retentionPolicy: {
          enabled: true
          days: 90
        }
      }
    ]
    logs: [
      {
        categoryGroup: 'audit'
        enabled: true
        retentionPolicy: {
          enabled: true
          days: 90
        }
      }
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

// Function App diagnostic settings
resource functionApp 'Microsoft.Web/sites@2023-12-01' existing = {
  name: resourceName
}

resource functionAppDiagnostics 'Microsoft.Insights/diagnosticsettings@2021-05-01-preview' = if (resourceType == 'FunctionApp') {
  name: diagnosticSettingName
  scope: functionApp
  properties: {
    workspaceId: logAnalyticsWorkspaceId
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
        category: 'FunctionAppLogs'
        enabled: true
        retentionPolicy: {
          enabled: true
          days: 90
        }
      }
      {
        category: 'AppServiceConsoleLogs'
        enabled: true
        retentionPolicy: {
          enabled: true
          days: 90
        }
      }
      {
        category: 'AppServiceHTTPLogs'
        enabled: true
        retentionPolicy: {
          enabled: true
          days: 90
        }
      }
      {
        category: 'AppServiceAuditLogs'
        enabled: true
        retentionPolicy: {
          enabled: true
          days: 90
        }
      }
    ]
  }
}

// Application Insights diagnostic settings
resource appInsights 'Microsoft.Insights/components@2020-02-02' existing = {
  name: resourceName
}

resource appInsightsDiagnostics 'Microsoft.Insights/diagnosticsettings@2021-05-01-preview' = if (resourceType == 'ApplicationInsights') {
  name: diagnosticSettingName
  scope: appInsights
  properties: {
    workspaceId: logAnalyticsWorkspaceId
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
        categoryGroup: 'audit'
        enabled: true
        retentionPolicy: {
          enabled: true
          days: 90
        }
      }
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

output diagnosticSettingName string = diagnosticSettingName