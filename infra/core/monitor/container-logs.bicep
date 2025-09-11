@description('The name of the web app to configure container logging for')
param webAppName string

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

output containerLoggingEnabled bool = enableContainerLogging
output logLevel string = logLevel
