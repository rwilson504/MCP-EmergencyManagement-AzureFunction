using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;
using Microsoft.Extensions.Logging;
using EmergencyManagementMCP.Services;
using EmergencyManagementMCP.Common;

namespace EmergencyManagementMCP.Tools
{
    public class ListFacilityIdsTool
    {
        private readonly EmergencyManagementService _service;
        private readonly ILogger<ListFacilityIdsTool> _logger;

        public ListFacilityIdsTool(EmergencyManagementService service, ILogger<ListFacilityIdsTool> logger)
        {
            _service = service;
            _logger = logger;
        }

        [Function(nameof(ListFacilityIdsTool))]
        public async Task<object> Run(
            [McpToolTrigger(ToolName, ToolDescription)] ListFacilityIdsRequest request,
            ToolInvocationContext context)
        {
            _logger.LogInformation("Received ListFacilityIds request: {Type}", request.type);
            var parameters = McpToolParameterHelper.BuildParametersFromRequest(request, Properties);
            var result = await _service.ListFacilityIdsAsync(parameters["type"]?.ToString());
            _logger.LogInformation("ListFacilityIds result: {Result}", result);
            return result;
        }

        public const string ToolName = "ListFacilityIds";
        public const string ToolDescription = "Provides a bulk list of all facility IDs, optionally filtered by type.";
        public static readonly IReadOnlyList<McpToolProperty> Properties = new List<McpToolProperty>
        {
            new McpToolProperty {
                Name = "type",
                Type = "string",
                Description = "Facility type to filter by (health, cemetery, benefits, vet_center)."
            }
        };
    }

    // POCO for Azure Function argument binding
    public class ListFacilityIdsRequest
    {
        public string? type { get; set; }
    }
}
