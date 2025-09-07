<!--
---
name: EmergencyManagement MCP Server on Azure functions
description: Run a remote Emergency Management MCP server on Azure functions.  
page_type: sample
languages:
- csharp
- bicep
- azdeveloper
products:
- azure-functions
- azure
urlFragment: remote-mcp-functions-dotnet
---
-->

<!-- Introduction: what this MCP server is dedicated to -->
## Introduction

Emergency management requires real-time, intelligent decision-making tools that can adapt to rapidly changing conditions.

This MCP server provides emergency management tools including facility search, service location, and fire-aware routing capabilities.

---


This MCP server is dedicated to helping emergency management professionals and applications with:

- **Fire Zone Checking**: Instantly determine if a street address or set of coordinates is located within an active wildfire perimeter, including incident details and containment status.
- **Facility and Service Search**: Find emergency facilities and services within specific areas or drive times, and retrieve detailed information about their capabilities.
- **Fire-Aware Routing**: Compute the shortest and safest routes between addresses or coordinates, automatically avoiding wildfire perimeters and road closures, with turn-by-turn driving directions.
- **Real-Time Data Access**: Integrate and access up-to-date emergency management data through standardized MCP APIs for rapid decision-making and situational awareness.

Use this server to integrate emergency management capabilities into your applications, ensuring responders have access to the most current information for effective decision-making.

## Available MCP Tools

### Fire Zone Checking Tools
- **emergency.addressFireZoneCheck** - Check if a street address is located within an active fire zone and get coordinates (address-based)
- **emergency.coordinateFireZoneCheck** - Check if latitude/longitude coordinates are located within an active fire zone

### Fire-Aware Routing Tools  
- **routing.addressFireAwareShortest** - Compute the shortest route between addresses while avoiding wildfire perimeters and closures
- **routing.coordinateFireAwareShortest** - Compute the shortest route while avoiding wildfire perimeters and closures (coordinate-based)

## Available MCP Tools

### Fire Zone Checking Tools
- **emergency.addressFireZoneCheck** - Check if a street address is located within an active fire zone and get coordinates (address-based)
- **emergency.coordinateFireZoneCheck** - Check if latitude/longitude coordinates are located within an active fire zone

### Fire-Aware Routing Tools  
- **routing.addressFireAwareShortest** - Compute the shortest route between addresses while avoiding wildfire perimeters and closures
- **routing.coordinateFireAwareShortest** - Compute the shortest route while avoiding wildfire perimeters and closures (coordinate-based)

# Getting Started with Remote MCP Servers using Azure Functions (.NET/C#)

This is a quickstart template to easily build and deploy a custom remote MCP server to the cloud using Azure functions. You can clone/restore/run on your local machine with debugging, and `azd up` to have it in the cloud in a couple minutes.  The MCP server is secured by design using keys and HTTPs, and allows more options for OAuth using EasyAuth and/or API Management as well as network isolation using VNET.  

**Watch the video overview**

<a href="https://www.youtube.com/watch?v=XwnEtZxaokg">
  <img src="./images/video-overview.png" alt="Watch the video" width="500" />
</a>

If you're looking for this sample in more languages check out the [Node.js/TypeScript](https://github.com/Azure-Samples/remote-mcp-functions-typescript) and [Python](https://github.com/Azure-Samples/remote-mcp-functions-python) samples.  

[![Open in GitHub Codespaces](https://github.com/codespaces/badge.svg)](https://codespaces.new/Azure-Samples/remote-mcp-functions-dotnet)

## Prerequisites

+ [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
+ [Azure Functions Core Tools](https://learn.microsoft.com/azure/azure-functions/functions-run-local?pivots=programming-language-csharp#install-the-azure-functions-core-tools) >= `4.0.7030`
+ [Azure Developer CLI](https://aka.ms/azd)
+ To use Visual Studio to run and debug locally:
  + [Visual Studio 2022](https://visualstudio.microsoft.com/vs/).
  + Make sure to select the **Azure development** workload during installation.
+ To use Visual Studio Code to run and debug locally:
  + [Visual Studio Code](https://code.visualstudio.com/)
  + [Azure Functions extension](https://marketplace.visualstudio.com/items?itemName=ms-azuretools.vscode-azurefunctions)

## Architecture Diagrams

This section provides visual representations of how the MCP Emergency Management Azure Function works, specifically focusing on the fire-aware routing functionality.

### Technical Architecture Diagram
For developers and technical stakeholders, see the detailed [Technical Architecture Diagram](technical-architecture.md) which shows:
- Azure resource connections and data flow
- Authentication and security mechanisms  
- External API integrations (ArcGIS, Azure Maps)
- Service layer components and dependencies

### Logical Flow Diagram  
For business users and non-technical stakeholders, see the [Logical Flow Diagram](logical-flow.md) which explains:
- Step-by-step user journey
- How fire-aware routing works in simple terms
- Why this architecture is powerful for emergency management
- Real-world benefits and use cases

Below is the original architecture diagram for the Remote MCP Server using Azure Functions:

![Architecture Diagram](architecture-diagram.png)

## Prepare your local environment

An Azure Storage Emulator is needed for this particular sample because we will save and get snippets from blob storage. 

1. Start Azurite

    ```shell
    docker run -p 10000:10000 -p 10001:10001 -p 10002:10002 \
        mcr.microsoft.com/azure-storage/azurite
    ```

>**Note** if you use Azurite coming from VS Code extension you need to run `Azurite: Start` now or you will see errors.

## Run your MCP Server locally from the terminal

1. From the `src` folder, run this command to start the Functions host locally:

    ```shell
    cd src
    func start
    ```

Note by default this will use the webhooks route: `/runtime/webhooks/mcp/sse`.  Later we will use this in Azure to set the key on client/host calls: `/runtime/webhooks/mcp/sse?code=<system_key>`

## Connect to the *local* MCP server from within a client/host

### VS Code - Copilot Edits

1. **Add MCP Server** from command palette and add URL to your running Function app's streamable endpoint:
    ```shell
    http://0.0.0.0:7071/runtime/webhooks/mcp
    ```
1. **List MCP Servers** from command palette and start the server
1. In Copilot chat agent mode enter a prompt to trigger the tool, e.g., select some code and enter this prompt

    ```plaintext
    Say Hello 
    ```

    ```plaintext
    Save this snippet as snippet1 
    ```

    ```plaintext
    Retrieve snippet1 and apply to NewFile.cs
    ```
1. When prompted to run the tool, consent by clicking **Continue**

1. When you're done, press Ctrl+C in the terminal window to stop the `func.exe` host process.

### MCP Inspector

1. In a **new terminal window**, install and run MCP Inspector

    ```shell
    npx @modelcontextprotocol/inspector node build/index.js
    ```

1. CTRL click to load the MCP Inspector web app from the URL displayed by the app (e.g. http://0.0.0.0:5173/#resources)
1. Set the transport type to `Streamable HTTP` 
1. Set the URL to your running Function app's streamable endpoint and **Connect**:
    ```shell
    http://0.0.0.0:7071/runtime/webhooks/mcp
    ```
1. **List Tools**.  Click on a tool and **Run Tool**.  

## Tool Documentation

### Fire-Aware Routing Tools

#### Address-Based Fire-Aware Routing (`routing.addressFireAwareShortest`)

Compute the shortest route between addresses while avoiding wildfire perimeters and closures.

**Parameters:**
```json
{
  "originAddress": "123 Main St, Los Angeles, CA",
  "destinationAddress": "456 Oak Ave, Pasadena, CA",
  "avoidBufferMeters": 2000,
  "departAtIsoUtc": "2025-09-05T13:00:00Z",
  "profile": "driving"
}
```

#### Coordinate-Based Fire-Aware Routing (`routing.coordinateFireAwareShortest`)

Compute the shortest route using coordinates while avoiding wildfire perimeters and closures.

**Parameters:**
```json
{
  "originLat": 34.0522,
  "originLon": -118.2437,
  "destinationLat": 34.1625,
  "destinationLon": -118.1331,
  "avoidBufferMeters": 2000,
  "departAtIsoUtc": "2025-09-05T13:00:00Z",
  "profile": "driving"
}
```

**Fire-Aware Routing Response:**
```json
{
  "route": {
    "distanceMeters": 15420,
    "travelTimeSeconds": 1140,
    "polylineGeoJson": "{\"type\":\"LineString\",\"coordinates\":[...]}",
    "drivingDirections": [
      {
        "routeOffsetInMeters": 0,
        "travelTimeInSeconds": 0,
        "point": { "lat": 34.0522, "lon": -118.2437 },
        "message": "Head north on Main St"
      }
    ]
  },
  "appliedAvoids": ["minLon,minLat,maxLon,maxLat"],
  "traceId": "abc12345"
}
```

**Key Features:**
- Fetches real-time wildfire perimeter data from ArcGIS services
- Caches fire data for 10 minutes to optimize performance
- Applies configurable buffer zones around fire perimeters
- Integrates with Azure Maps for route calculation
- Returns GeoJSON LineString for route visualization
- Provides turn-by-turn driving directions

### Fire Zone Checking Tools

#### Address-Based Fire Zone Check (`emergency.addressFireZoneCheck`)

Check if a street address is located within an active fire zone and get coordinates.

**Parameters:**
```json
{
  "address": "123 Wildfire Road, Paradise, CA 95969"
}
```

**Response:**
```json
{
  "geocoding": {
    "address": "123 Wildfire Road, Paradise, CA 95969",
    "coordinates": { "lat": 39.7596, "lon": -121.6219 },
    "confidence": "High",
    "formattedAddress": "123 Wildfire Road, Paradise, CA 95969, United States"
  },
  "fireZone": {
    "isInFireZone": true,
    "fireZoneName": "Camp Fire",
    "incidentName": "Camp Fire Incident",
    "containmentPercent": 85.0,
    "acresBurned": 153336.0,
    "lastUpdate": "2024-01-15T14:30:00Z"
  },
  "traceId": "abc12345"
}
```

#### Coordinate-Based Fire Zone Check (`emergency.coordinateFireZoneCheck`)

Check if latitude/longitude coordinates are located within an active fire zone.

**Parameters:**
```json
{
  "lat": 39.7596,
  "lon": -121.6219
}
```

**Response:**
```json
{
  "coordinates": { "lat": 39.7596, "lon": -121.6219 },
  "fireZone": {
    "isInFireZone": true,
    "fireZoneName": "Camp Fire",
    "incidentName": "Camp Fire Incident",
    "containmentPercent": 85.0,
    "acresBurned": 153336.0,
    "lastUpdate": "2024-01-15T14:30:00Z"
  },
  "traceId": "abc12345"
}
```

**Fire Zone Check Key Features:**
- Geocodes addresses using Azure Maps Search API with high accuracy
- Checks coordinates against real-time wildfire perimeter data from ArcGIS
- Provides detailed fire zone information including incident name, containment, and size
- Caches fire data for 10 minutes to optimize performance

## Deploy to Azure for Remote MCP

Run this [azd](https://aka.ms/azd) command to provision the function app, with any required Azure resources, and deploy your code:

```shell
azd up
```

**Infrastructure Components:**

The deployment includes the following Azure resources for emergency management capabilities:

- **Azure Functions** - Hosts the MCP server with .NET 8 isolated runtime
- **Azure Storage Account** - Provides blob storage for:
  - Function deployment packages
  - Code snippets storage
  - **Geo cache container** - Caches wildfire perimeter GeoJSON data with TTL
- **Azure Maps Account** - Provides routing services for fire-aware route calculations
- **Application Insights** - Monitoring and logging for the MCP server
- **Managed Identity** - Secure access between services without storing credentials

**Configuration:**

The deployment automatically configures:
- Blob storage container (`routing-cache`) for geographic data caching
- Azure Maps integration with primary key injection
- Managed Identity with appropriate RBAC permissions
- Fire perimeter data source (ArcGIS/NIFC) endpoints

You can opt-in to a VNet being used in the sample. To do so, do this before `azd up`

```bash
azd env set VNET_ENABLED true
```

Additionally, [API Management](https://learn.microsoft.com/en-us/azure/api-management/) can be used for improved security and policies over your MCP Server, and [App Service built-in authentication](https://learn.microsoft.com/en-us/azure/app-service/overview-authentication-authorization) can be used to set up your favorite OAuth provider including Entra.  

## Connect to your *remote() MCP server function app from a client

Your client will need a key in order to invoke the new hosted streamable endpoint, which will be of the form `https://<funcappname>.azurewebsites.net/runtime/webhooks/mcp`. The hosted function requires a system key by default which can be obtained from the [portal](https://learn.microsoft.com/en-us/azure/azure-functions/function-keys-how-to?tabs=azure-portal) or the CLI (`az functionapp keys list --resource-group <resource_group> --name <function_app_name>`). Obtain the system key named `mcp_extension`.

### Connect to remote MCP server in MCP Inspector
For MCP Inspector, you can include the key in the URL: 
```plaintext
https://<funcappname>.azurewebsites.net/runtime/webhooks/mcp?code=<your-mcp-extension-system-key>
```

### Connect to remote MCP server in VS Code - GitHub Copilot
For GitHub Copilot within VS Code, you should instead set the key as the `x-functions-key` header in `mcp.json`, and you would just use `https://<funcappname>.azurewebsites.net/runtime/webhooks/mcp` for the URL. The following example uses an input and will prompt you to provide the key when you start the server from VS Code.  Note [mcp.json]() has already been included in this repo and will be picked up by VS Code.  Click Start on the server to be prompted for values including `functionapp-name` (in your /.azure/*/.env file) and `functions-mcp-extension-system-key` which can be obtained from CLI command above or API Keys in the portal for the Function App.  

```json
{
    "inputs": [
        {
            "type": "promptString",
            "id": "functions-mcp-extension-system-key",
            "description": "Azure Functions MCP Extension System Key",
            "password": true
        },
        {
            "type": "promptString",
            "id": "functionapp-name",
            "description": "Azure Functions App Name"
        }
    ],
    "servers": {
        "remote-mcp-function": {
            "type": "http",
            "url": "https://${input:functionapp-name}.azurewebsites.net/runtime/webhooks/mcp",
            "headers": {
                "x-functions-key": "${input:functions-mcp-extension-system-key}"
            }
        },
        "local-mcp-function": {
            "type": "http",
            "url": "http://0.0.0.0:7071/runtime/webhooks/mcp"
        }
    }
}
```

Click Start on the server to be prompted for values including `functionapp-name` (in your /.azure/*/.env file) and `functions-mcp-extension-system-key` which can be obtained from CLI command above or API Keys in the portal for the Function App.

## Redeploy your code

You can run the `azd up` command as many times as you need to both provision your Azure resources and deploy code updates to your function app.

>[!NOTE]
>Deployed code files are always overwritten by the latest deployment package.

## Debugging and Logging

This MCP server includes comprehensive logging to help diagnose issues in both local development and production environments. The logging system provides detailed information about request processing, external service calls, and error conditions.

### Enhanced Logging Features

- **Request Correlation**: Each request gets a unique request ID that traces through all services
- **Performance Monitoring**: Timing information for all major operations
- **Detailed Error Context**: Comprehensive error messages with categorization (HTTP errors, timeouts, JSON parsing, etc.)
- **Security-Aware Logging**: Sensitive information like API keys are excluded from logs
- **Service Dependency Tracking**: Detailed logging for Azure Maps API, ArcGIS services, and Azure Storage operations

### Log Levels and Configuration

The application uses structured logging with different levels:

- **Debug**: Detailed execution flow, parameter values, and step-by-step processing
- **Information**: Key operations, successful completions, and performance metrics
- **Warning**: Non-critical issues, fallbacks, and truncated data
- **Error**: Exceptions, service failures, and critical issues

#### Local Development Logging

For local debugging, copy `local.settings.json.default` to `local.settings.json` and configure the log levels:

```json
{
  "Host": {
    "logging": {
      "logLevel": {
        "default": "Information",
        "EmergencyManagementMCP": "Debug",
        "Microsoft.Azure.Functions.Worker": "Information",
        "Azure.Storage": "Information"
      },
      "console": {
        "isEnabled": true,
        "includeScopes": true
      }
    }
  }
}
```

#### Production Logging

Production logging is configured in `host.json` with Application Insights integration:

```json
{
  "logging": {
    "logLevel": {
      "default": "Information",
      "EmergencyManagementMCP": "Debug"
    },
    "applicationInsights": {
      "enableDependencyTracking": true,
      "enablePerformanceCountersCollection": true
    }
  }
}
```

### Common Debugging Scenarios

#### Route Calculation Issues

Look for these log messages when debugging routing problems:

```
[Information] Starting fire-aware routing request: origin=(lat,lon), destination=(lat,lon), traceId=abc123
[Debug] Step 1: Computing bounding box, traceId=abc123
[Debug] Step 3: Loading fire perimeter data, traceId=abc123
[Debug] Step 7: Calculating route with X avoid areas, traceId=abc123
```

#### Azure Maps API Issues

Monitor for these error patterns:

```
[Error] Azure Maps API returned error 401: Invalid subscription key, requestId=def456
[Warning] Truncated avoid areas from 15 to 10 due to Azure Maps limit, requestId=def456
```

#### Storage/Cache Issues

Watch for storage-related errors:

```
[Error] Azure storage error in cache operation: key=fire-perimeters-123, statusCode=403, requestId=ghi789
[Warning] Falling back to refresher due to cache error, requestId=ghi789
```

### Monitoring in Production

1. **Application Insights**: View detailed telemetry, dependency tracking, and performance metrics
2. **Log Analytics**: Query structured logs for troubleshooting specific issues
3. **Function App Logs**: Stream real-time logs during development and testing

#### Sample Log Analytics Queries

Find all errors for a specific operation:
```kusto
traces
| where severityLevel >= 3
| where message contains "fire-aware routing"
| order by timestamp desc
```

Track performance of route calculations:
```kusto
traces
| where message contains "Route calculated successfully"
| extend duration = extract(@"elapsed=(\d+)ms", 1, message)
| summarize avg(toint(duration)) by bin(timestamp, 1h)
```

### Troubleshooting Common Issues

1. **Missing Configuration**: Check `local.settings.json` for required Azure Maps keys and storage URLs
2. **Authentication Failures**: Verify managed identity permissions for storage and external APIs
3. **Network Timeouts**: Monitor network latency to external services (ArcGIS, Azure Maps)
4. **Cache Performance**: Review cache hit/miss ratios and storage latency

For additional debugging, enable Debug log level for the `EmergencyManagementMCP` namespace to see detailed request processing steps.

## Testing

The project includes comprehensive integration tests to validate functionality and support development workflows.

### Running Tests

Run all tests from the solution root:
```bash
dotnet test
```

Run tests with detailed output for debugging:
```bash
dotnet test --logger "console;verbosity=detailed"
```

Run a specific test class:
```bash
dotnet test --filter "ClassName=AddressFireZoneCheckTest"
```

### Test Coverage

The integration tests cover:
- **API Integration**: URL construction and parameter validation for ArcGIS and Azure Maps APIs
- **Model Validation**: Data structure integrity and JSON serialization/deserialization
- **Coordinate Processing**: Bounding box calculations and coordinate transformations
- **Response Parsing**: JSON response extraction and driving direction processing
- **Business Logic**: Address fire zone checking and geocoding workflows

### Test Project Structure

All tests are located in `tests/EmergencyManagementMcp.IntegrationTests/`:
- **AddressFireZoneCheckTest** - Core address fire zone checking functionality
- **ArcGisUrlConstructionTest** - ArcGIS API URL and parameter validation
- **RouterClientApiFormatTest** - Azure Maps API request formatting
- **DrivingDirectionsTest** - JSON response parsing and instruction extraction
- **CaliforniaBoundingBoxTest** - Geographic coordinate handling
- **CoordinateTransformationDebugTest** - Coordinate transformation debugging
- **StandaloneAddressFireZoneTest** - Model validation and data flow testing

For more details, see the [test project README](tests/EmergencyManagementMcp.IntegrationTests/README.md).

## Clean up resources

When you're done working with your function app and related resources, you can use this command to delete the function app and its related resources from Azure and avoid incurring any further costs:

```shell
azd down
```
