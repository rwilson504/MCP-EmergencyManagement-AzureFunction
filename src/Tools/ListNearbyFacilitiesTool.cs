using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;
using Microsoft.Extensions.Logging;
using EmergencyManagementMCP.Services;
using EmergencyManagementMCP.Common;

namespace EmergencyManagementMCP.Tools
{
    public class ListNearbyFacilitiesTool
    {
        private readonly EmergencyManagementService _service;
        private readonly ILogger<ListNearbyFacilitiesTool> _logger;

        public ListNearbyFacilitiesTool(EmergencyManagementService service, ILogger<ListNearbyFacilitiesTool> logger)
        {
            _service = service;
            _logger = logger;
        }

        [Function(nameof(ListNearbyFacilitiesTool))]
        public async Task<object> Run(
            [McpToolTrigger(ToolName, ToolDescription)] ListNearbyFacilitiesRequest request,
            ToolInvocationContext context)
        {
            _logger.LogInformation("Received ListNearbyFacilities request: lat={Lat}, long={Long}, drive_time={DriveTime}, services={Services}, page={Page}, per_page={PerPage}",
                request.lat, request.@long, request.drive_time, request.services, request.page, request.per_page);
            var parameters = McpToolParameterHelper.BuildParametersFromRequest(request, Properties);
            var result = await _service.ListNearbyFacilitiesAsync(
                (double)parameters["lat"],
                (double)parameters["long"],
                parameters["drive_time"] as int?,
                parameters["services"]?.ToString(),
                parameters["page"] as int?,
                parameters["per_page"] as int?
            );
            _logger.LogInformation("ListNearbyFacilities result: {Result}", result);
            return result;
        }

        public const string ToolName = "ListNearbyFacilities";
        public const string ToolDescription = "Find facilities within a specified drive time from a location, with optional service filtering.";

        public static readonly IReadOnlyList<McpToolProperty> Properties = new List<McpToolProperty>
        {
            new McpToolProperty { Name = "lat", Type = "number", Description = "Latitude from which drive time is calculated." },
            new McpToolProperty { Name = "long", Type = "number", Description = "Longitude from which drive time is calculated." },
            new McpToolProperty { Name = "drive_time", Type = "integer", Description = "Maximum drive time in minutes from the location to include facilities." },
            new McpToolProperty { Name = "services", Type = "string", Description = "An optional comma separated list of service to filter by." },
            new McpToolProperty { Name = "page", Type = "integer", Description = "The page number of results to return." },
            new McpToolProperty { Name = "per_page", Type = "integer", Description = "The number of results to return per page." }
        };
    }

    // POCO for Azure Function argument binding
    public class ListNearbyFacilitiesRequest
    {
        public double lat { get; set; }
        public double @long { get; set; }
        public int? drive_time { get; set; }
        public string? services { get; set; }
        public int? page { get; set; }
        public int? per_page { get; set; }
    }
}
