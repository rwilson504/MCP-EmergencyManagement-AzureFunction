using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using EmergencyManagementMCP.Tools;
using EmergencyManagementMCP.Services;

var builder = FunctionsApplication.CreateBuilder(args);
builder.ConfigureFunctionsWebApplication();
builder.EnableMcpToolMetadata();

// MCP Tool: Fire-Aware Routing
var routingFireAwareShortest = builder.ConfigureMcpTool(RoutingFireAwareShortestTool.ToolName);
foreach (var prop in RoutingFireAwareShortestTool.Properties)
    routingFireAwareShortest.WithProperty(prop.Name, prop.Type, prop.Description);

// Register Fire-Aware Routing Services
builder.Services.AddHttpClient<IGeoServiceClient, GeoServiceClient>();
builder.Services.AddHttpClient<IRouterClient, RouterClient>();
builder.Services.AddSingleton<IGeoJsonCache, GeoJsonCache>();
builder.Services.AddSingleton<IGeometryUtils, GeometryUtils>();

builder.Build().Run();