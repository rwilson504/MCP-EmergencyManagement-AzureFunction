using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;
using Microsoft.Extensions.Logging;
using VeteransAffairsMCP.Services;
using VeteransAffairsMCP.Common;

namespace VeteransAffairsMCP.Tools
{
    public class ListFacilityServicesTool
    {
        private readonly VeteransAffairsService _service;
        private readonly ILogger<ListFacilityServicesTool> _logger;

        public ListFacilityServicesTool(VeteransAffairsService service, ILogger<ListFacilityServicesTool> logger)
        {
            _service = service;
            _logger = logger;
        }

        [Function(nameof(ListFacilityServicesTool))]
        public async Task<object> Run(
            [McpToolTrigger(ToolName, ToolDescription)] ListFacilityServicesRequest request,
            ToolInvocationContext context)
        {
            _logger.LogInformation("Received ListFacilityServices request: facilityId={FacilityId}, serviceIds={ServiceIds}, serviceType={ServiceType}",
                request.facilityId, request.serviceIds, request.serviceType);
            var parameters = McpToolParameterHelper.BuildParametersFromRequest(request, Properties);
            var result = await _service.ListFacilityServicesAsync(
                parameters["facilityId"]?.ToString(),
                parameters["serviceIds"]?.ToString(),
                parameters["serviceType"]?.ToString()
            );
            _logger.LogInformation("ListFacilityServices result: {Result}", result);
            return result;
        }

        public const string ToolName = "ListFacilityServices";
        public const string ToolDescription = "List all services offered by a specific facility, with optional filtering by service IDs or type.";
        public static readonly IReadOnlyList<McpToolProperty> Properties = new List<McpToolProperty>
        {
            new McpToolProperty {
                Name = "facilityId",
                Type = "string",
                Description = "The unique identifier of the facility, e.g., 'vha_688'."
            },
            new McpToolProperty {
                Name = "serviceIds",
                Type = "string",
                Description = "A comma separated list of service IDs to filter the search."
            },
            new McpToolProperty {
                Name = "serviceType",
                Type = "string",
                Description = "Service type to filter the search."
            }
        };
    }

    // POCO for Azure Function argument binding
    public class ListFacilityServicesRequest
    {
        public string facilityId { get; set; } = string.Empty;
        public string? serviceIds { get; set; }
        public string? serviceType { get; set; }
    }
}
