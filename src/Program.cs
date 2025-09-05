using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using EmergencyManagementMCP.Tools;
using EmergencyManagementMCP.Services;

var builder = FunctionsApplication.CreateBuilder(args);
builder.ConfigureFunctionsWebApplication();
builder.EnableMcpToolMetadata();

// MCP Tool: List Facilities
var listFacilities = builder.ConfigureMcpTool(ListFacilitiesTool.ToolName);
foreach (var prop in ListFacilitiesTool.Properties)
    listFacilities.WithProperty(prop.Name, prop.Type, prop.Description);

// MCP Tool: Get Facility By ID
var getFacilityById = builder.ConfigureMcpTool(GetFacilityByIdTool.ToolName);
foreach (var prop in GetFacilityByIdTool.Properties)
    getFacilityById.WithProperty(prop.Name, prop.Type, prop.Description);

// MCP Tool: List Facility Services
var listFacilityServices = builder.ConfigureMcpTool(ListFacilityServicesTool.ToolName);
foreach (var prop in ListFacilityServicesTool.Properties)
    listFacilityServices.WithProperty(prop.Name, prop.Type, prop.Description);

// MCP Tool: Get Facility Service Details
var getFacilityServiceDetails = builder.ConfigureMcpTool(GetFacilityServiceDetailsTool.ToolName);
foreach (var prop in GetFacilityServiceDetailsTool.Properties)
    getFacilityServiceDetails.WithProperty(prop.Name, prop.Type, prop.Description);

// MCP Tool: List Facility IDs
var listFacilityIds = builder.ConfigureMcpTool(ListFacilityIdsTool.ToolName);
foreach (var prop in ListFacilityIdsTool.Properties)
    listFacilityIds.WithProperty(prop.Name, prop.Type, prop.Description);

// MCP Tool: List Nearby Facilities
var listNearbyFacilities = builder.ConfigureMcpTool(ListNearbyFacilitiesTool.ToolName);
foreach (var prop in ListNearbyFacilitiesTool.Properties)
    listNearbyFacilities.WithProperty(prop.Name, prop.Type, prop.Description);

// Register EmergencyManagementService with DI, including HttpClient and IConfiguration
builder.Services.AddHttpClient<EmergencyManagementService>();
builder.Services.AddTransient<EmergencyManagementService>(sp =>
{
    var httpClient = sp.GetRequiredService<HttpClient>();
    var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<EmergencyManagementService>>();
    var config = sp.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>();
    return new EmergencyManagementService(httpClient, logger, config);
});

builder.Build().Run();