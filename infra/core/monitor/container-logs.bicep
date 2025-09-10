@description('The name of the web app to configure container logging for')
param webAppName string

@description('The resource group where the web app is located')
param resourceGroupName string

@description('The Log Analytics workspace ID to send container logs to')
param logAnalyticsWorkspaceId string

@description('Enable container logging')
param enableContainerLogging bool = true

@description('Container logging level')
@allowed(['Error', 'Warning', 'Info', 'Verbose'])
param logLevel string = 'Verbose'

// Reference the existing web app
resource webApp 'Microsoft.Web/sites@2023-12-01' existing = {
  name: webAppName
}

// Configure container logging settings
resource containerLogging 'Microsoft.Web/sites/config@2023-12-01' = if (enableContainerLogging) {
  name: 'logs'
  parent: webApp
  properties: {
    applicationLogs: {
      fileSystem: {
        level: logLevel
      }
      azureBlobStorage: {
        level: 'Off'
      }
    }
    httpLogs: {
      fileSystem: {
        retentionInMb: 35
        retentionInDays: 7
        enabled: true
      }
    }
    failedRequestsTracing: {
      enabled: true
    }
    detailedErrorMessages: {
      enabled: true
    }
  }
}

// Enable Docker container logging specifically for Linux containers
resource dockerContainerLogging 'Microsoft.Web/sites/config@2023-12-01' = if (enableContainerLogging) {
  name: 'appsettings'
  parent: webApp
  properties: {
    WEBSITES_ENABLE_APP_SERVICE_STORAGE: 'true'
    DOCKER_ENABLE_CI: 'true'
    WEBSITES_CONTAINER_START_TIME_LIMIT: '1800'
  }
  dependsOn: [
    containerLogging
  ]
}

output containerLoggingEnabled bool = enableContainerLogging
output logLevel string = logLevel