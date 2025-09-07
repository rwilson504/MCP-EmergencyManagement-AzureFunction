using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using EmergencyManagementMCP.Tools;
using EmergencyManagementMCP.Services;

var builder = FunctionsApplication.CreateBuilder(args);
builder.ConfigureFunctionsWebApplication();
builder.EnableMcpToolMetadata();

// Configure Application Insights for production monitoring
builder.Services.AddApplicationInsightsTelemetryWorkerService();

// Configure enhanced logging
builder.Services.Configure<LoggerFilterOptions>(options =>
{
    // Set default log level to Information for better debugging
    options.MinLevel = LogLevel.Information;
});

// MCP Tool: Fire-Aware Routing (Coordinate-based)
var routingFireAwareShortest = builder.ConfigureMcpTool(CoordinateRoutingFireAwareShortestTool.ToolName);
routingFireAwareShortest
    .WithProperty(CoordinateRoutingFireAwareShortestToolPropertyStrings.OriginLatName, CoordinateRoutingFireAwareShortestToolPropertyStrings.OriginLatType, CoordinateRoutingFireAwareShortestToolPropertyStrings.OriginLatDescription)
    .WithProperty(CoordinateRoutingFireAwareShortestToolPropertyStrings.OriginLonName, CoordinateRoutingFireAwareShortestToolPropertyStrings.OriginLonType, CoordinateRoutingFireAwareShortestToolPropertyStrings.OriginLonDescription)
    .WithProperty(CoordinateRoutingFireAwareShortestToolPropertyStrings.DestinationLatName, CoordinateRoutingFireAwareShortestToolPropertyStrings.DestinationLatType, CoordinateRoutingFireAwareShortestToolPropertyStrings.DestinationLatDescription)
    .WithProperty(CoordinateRoutingFireAwareShortestToolPropertyStrings.DestinationLonName, CoordinateRoutingFireAwareShortestToolPropertyStrings.DestinationLonType, CoordinateRoutingFireAwareShortestToolPropertyStrings.DestinationLonDescription)
    .WithProperty(CoordinateRoutingFireAwareShortestToolPropertyStrings.AvoidBufferMetersName, CoordinateRoutingFireAwareShortestToolPropertyStrings.AvoidBufferMetersType, CoordinateRoutingFireAwareShortestToolPropertyStrings.AvoidBufferMetersDescription)
    .WithProperty(CoordinateRoutingFireAwareShortestToolPropertyStrings.DepartAtIsoUtcName, CoordinateRoutingFireAwareShortestToolPropertyStrings.DepartAtIsoUtcType, CoordinateRoutingFireAwareShortestToolPropertyStrings.DepartAtIsoUtcDescription)
    .WithProperty(CoordinateRoutingFireAwareShortestToolPropertyStrings.ProfileName, CoordinateRoutingFireAwareShortestToolPropertyStrings.ProfileType, CoordinateRoutingFireAwareShortestToolPropertyStrings.ProfileDescription);

// MCP Tool: Fire-Aware Routing (Address-based)
var addressRoutingFireAwareShortest = builder.ConfigureMcpTool(AddressRoutingFireAwareShortestTool.ToolName);
addressRoutingFireAwareShortest
    .WithProperty(AddressRoutingFireAwareShortestToolPropertyStrings.OriginAddressName, AddressRoutingFireAwareShortestToolPropertyStrings.OriginAddressType, AddressRoutingFireAwareShortestToolPropertyStrings.OriginAddressDescription)
    .WithProperty(AddressRoutingFireAwareShortestToolPropertyStrings.DestinationAddressName, AddressRoutingFireAwareShortestToolPropertyStrings.DestinationAddressType, AddressRoutingFireAwareShortestToolPropertyStrings.DestinationAddressDescription)
    .WithProperty(AddressRoutingFireAwareShortestToolPropertyStrings.AvoidBufferMetersName, AddressRoutingFireAwareShortestToolPropertyStrings.AvoidBufferMetersType, AddressRoutingFireAwareShortestToolPropertyStrings.AvoidBufferMetersDescription)
    .WithProperty(AddressRoutingFireAwareShortestToolPropertyStrings.DepartAtIsoUtcName, AddressRoutingFireAwareShortestToolPropertyStrings.DepartAtIsoUtcType, AddressRoutingFireAwareShortestToolPropertyStrings.DepartAtIsoUtcDescription)
    .WithProperty(AddressRoutingFireAwareShortestToolPropertyStrings.ProfileName, AddressRoutingFireAwareShortestToolPropertyStrings.ProfileType, AddressRoutingFireAwareShortestToolPropertyStrings.ProfileDescription);

// MCP Tool: Address Fire Zone Check
var addressFireZoneCheck = builder.ConfigureMcpTool(AddressFireZoneCheckTool.ToolName);
addressFireZoneCheck
    .WithProperty(AddressFireZoneCheckToolPropertyStrings.AddressName, AddressFireZoneCheckToolPropertyStrings.AddressType, AddressFireZoneCheckToolPropertyStrings.AddressDescription);

// MCP Tool: Coordinate Fire Zone Check  
var coordinateFireZoneCheck = builder.ConfigureMcpTool(CoordinateFireZoneCheckTool.ToolName);
coordinateFireZoneCheck
    .WithProperty(CoordinateFireZoneCheckToolPropertyStrings.LatName, CoordinateFireZoneCheckToolPropertyStrings.LatType, CoordinateFireZoneCheckToolPropertyStrings.LatDescription)
    .WithProperty(CoordinateFireZoneCheckToolPropertyStrings.LonName, CoordinateFireZoneCheckToolPropertyStrings.LonType, CoordinateFireZoneCheckToolPropertyStrings.LonDescription);

// Register Fire-Aware Routing Services
builder.Services.AddHttpClient<IGeoServiceClient, GeoServiceClient>();
builder.Services.AddHttpClient<IRouterClient, RouterClient>();
builder.Services.AddHttpClient<IGeocodingClient, GeocodingClient>();
builder.Services.AddSingleton<IGeoJsonCache, GeoJsonCache>();
builder.Services.AddSingleton<IGeometryUtils, GeometryUtils>();

var app = builder.Build();

// Add startup logging
var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("Emergency Management MCP Function App starting up...");
logger.LogInformation("Application Insights configured: {AppInsightsEnabled}", builder.Services.Any(s => s.ServiceType.Name.Contains("ApplicationInsights")));

try
{
    logger.LogInformation("Emergency Management MCP Function App started successfully");
    app.Run();
}
catch (Exception ex)
{
    logger.LogCritical(ex, "Emergency Management MCP Function App failed to start");
    throw;
}