// Optional diagnostic settings deployment
// Deploy this after main infrastructure is deployed
// Usage: az deployment group create --resource-group <rg> --template-file diagnostics-deployment.bicep --parameters @diagnostics.parameters.json

@description('Storage account name')
param storageAccountName string

@description('Function app name') 
param functionAppName string

@description('Application Insights name')
param applicationInsightsName string

@description('Log Analytics workspace resource ID')
param logAnalyticsWorkspaceId string

// Deploy diagnostic settings for Storage Account
module storageDiagnostics './core/monitor/diagnostics.bicep' = {
  name: 'storageDiagnostics'
  params: {
    resourceName: storageAccountName
    logAnalyticsWorkspaceId: logAnalyticsWorkspaceId
    resourceType: 'StorageAccount'
    diagnosticSettingName: 'storage-security-diagnostics'
  }
}

// Deploy diagnostic settings for Function App
module functionAppDiagnostics './core/monitor/diagnostics.bicep' = {
  name: 'functionAppDiagnostics'
  params: {
    resourceName: functionAppName
    logAnalyticsWorkspaceId: logAnalyticsWorkspaceId
    resourceType: 'FunctionApp'
    diagnosticSettingName: 'functionapp-security-diagnostics'
  }
}

// Deploy diagnostic settings for Application Insights
module appInsightsDiagnostics './core/monitor/diagnostics.bicep' = {
  name: 'appInsightsDiagnostics'
  params: {
    resourceName: applicationInsightsName
    logAnalyticsWorkspaceId: logAnalyticsWorkspaceId
    resourceType: 'ApplicationInsights'
    diagnosticSettingName: 'appinsights-security-diagnostics'
  }
}

output deploymentComplete bool = true