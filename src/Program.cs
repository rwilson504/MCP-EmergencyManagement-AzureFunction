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

// MCP Tool: Fire-Aware Routing
var routingFireAwareShortest = builder.ConfigureMcpTool(RoutingFireAwareShortestTool.ToolName);
routingFireAwareShortest
    .WithProperty(RoutingFireAwareShortestToolPropertyStrings.OriginLatName, RoutingFireAwareShortestToolPropertyStrings.OriginLatType, RoutingFireAwareShortestToolPropertyStrings.OriginLatDescription)
    .WithProperty(RoutingFireAwareShortestToolPropertyStrings.OriginLonName, RoutingFireAwareShortestToolPropertyStrings.OriginLonType, RoutingFireAwareShortestToolPropertyStrings.OriginLonDescription)
    .WithProperty(RoutingFireAwareShortestToolPropertyStrings.DestinationLatName, RoutingFireAwareShortestToolPropertyStrings.DestinationLatType, RoutingFireAwareShortestToolPropertyStrings.DestinationLatDescription)
    .WithProperty(RoutingFireAwareShortestToolPropertyStrings.DestinationLonName, RoutingFireAwareShortestToolPropertyStrings.DestinationLonType, RoutingFireAwareShortestToolPropertyStrings.DestinationLonDescription)
    .WithProperty(RoutingFireAwareShortestToolPropertyStrings.AvoidBufferMetersName, RoutingFireAwareShortestToolPropertyStrings.AvoidBufferMetersType, RoutingFireAwareShortestToolPropertyStrings.AvoidBufferMetersDescription)
    .WithProperty(RoutingFireAwareShortestToolPropertyStrings.DepartAtIsoUtcName, RoutingFireAwareShortestToolPropertyStrings.DepartAtIsoUtcType, RoutingFireAwareShortestToolPropertyStrings.DepartAtIsoUtcDescription)
    .WithProperty(RoutingFireAwareShortestToolPropertyStrings.ProfileName, RoutingFireAwareShortestToolPropertyStrings.ProfileType, RoutingFireAwareShortestToolPropertyStrings.ProfileDescription);

// MCP Tool: Address Fire Zone Check
var addressFireZoneCheck = builder.ConfigureMcpTool(AddressFireZoneCheckTool.ToolName);
addressFireZoneCheck
    .WithProperty(AddressFireZoneCheckToolPropertyStrings.AddressName, AddressFireZoneCheckToolPropertyStrings.AddressType, AddressFireZoneCheckToolPropertyStrings.AddressDescription);

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