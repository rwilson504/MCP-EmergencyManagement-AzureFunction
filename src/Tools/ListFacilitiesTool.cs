using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;
using Microsoft.Extensions.Logging;
using EmergencyManagementMCP.Services;
using EmergencyManagementMCP.Common;

namespace EmergencyManagementMCP.Tools
{
    public class ListFacilitiesTool
    {
        private readonly EmergencyManagementService _service;
        private readonly ILogger<ListFacilitiesTool> _logger;

        public ListFacilitiesTool(EmergencyManagementService service, ILogger<ListFacilitiesTool> logger)
        {
            _service = service;
            _logger = logger;
        }

        [Function(nameof(ListFacilitiesTool))]
        public async Task<object> Run(
            [McpToolTrigger(ToolName, ToolDescription)] ListFacilitiesRequest request,
            ToolInvocationContext context)
        {
            _logger.LogInformation("Received ListFacilities request: {Request}", request);
            var parameters = McpToolParameterHelper.BuildParametersFromRequest(request, Properties);
            var result = await _service.ListFacilitiesAsync(parameters);
            _logger.LogInformation("ListFacilities result: {Result}", result);
            return result;
        }

        public const string ToolName = "ListFacilities";
        public const string ToolDescription = "Query VA facilities using various filters (IDs, zip, state, coordinates, radius, type, services, etc.).";
        public static readonly IReadOnlyList<McpToolProperty> Properties = new List<McpToolProperty>
        {
            new McpToolProperty { Name = "facilityIds", Type = "string", Description = "Comma separated list of facility IDs for the search." },
            new McpToolProperty { Name = "zip", Type = "string", Description = "Zip code for searching facilities, considering only the first five digits." },
            new McpToolProperty { Name = "state", Type = "string", Description = "State for facility search, typically a two-character code." },
            new McpToolProperty { Name = "lat", Type = "number", Description = "Latitude for facility search in WGS84 coordinates." },
            new McpToolProperty { Name = "long", Type = "number", Description = "Longitude for facility search in WGS84 coordinates." },
            new McpToolProperty { Name = "radius", Type = "number", Description = "Radial distance from specified point to filter facility search, in miles." },
            new McpToolProperty { Name = "bbox", Type = "string", Description = "Bounding box coordinates for facility search within a geographic area." },
            new McpToolProperty { Name = "visn", Type = "number", Description = "VISN (Veterans Integrated Service Networks) code for facility search." },
            new McpToolProperty { Name = "type", Type = "string", Description = "Type of facility location to filter search results." },
            new McpToolProperty { Name = "services", Type = "string", Description = "A comma separated list of services to filter facilities by the services they offer." },
            new McpToolProperty { Name = "mobile", Type = "boolean", Description = "Flag to include or exclude mobile facilities in the search results." },
            new McpToolProperty { Name = "page", Type = "integer", Description = "Page number of results to return in a paginated response." },
            new McpToolProperty { Name = "per_page", Type = "integer", Description = "Number of results per page in a paginated response." }
        };
    }

    // POCO for Azure Function argument binding
    public class ListFacilitiesRequest
    {
        public string? facilityIds { get; set; }
        public string? zip { get; set; }
        public string? state { get; set; }
        public double? lat { get; set; }
        public double? @long { get; set; }
        public double? radius { get; set; }
        public string? bbox { get; set; }
        public int? visn { get; set; }
        public string? type { get; set; }
        public string? services { get; set; }
        public bool? mobile { get; set; }
        public int? page { get; set; }
        public int? per_page { get; set; }
    }
}
