using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;
using Microsoft.Extensions.Logging;
using EmergencyManagementMCP.Services;
using EmergencyManagementMCP.Common;

namespace EmergencyManagementMCP.Tools
{
    public class GetFacilityServiceDetailsTool
    {
        private readonly EmergencyManagementService _service;
        private readonly ILogger<GetFacilityServiceDetailsTool> _logger;

        public GetFacilityServiceDetailsTool(EmergencyManagementService service, ILogger<GetFacilityServiceDetailsTool> logger)
        {
            _service = service;
            _logger = logger;
        }

        [Function(nameof(GetFacilityServiceDetailsTool))]
        public async Task<object> Run(
            [McpToolTrigger(ToolName, ToolDescription)] GetFacilityServiceDetailsRequest request,
            ToolInvocationContext context)
        {
            _logger.LogInformation("Received GetFacilityServiceDetails request: facilityId={FacilityId}, serviceId={ServiceId}",
                request.facilityId, request.serviceId);
            var parameters = McpToolParameterHelper.BuildParametersFromRequest(request, Properties);
            var result = await _service.GetFacilityServiceDetailsAsync(
                parameters["facilityId"]?.ToString(),
                parameters["serviceId"]?.ToString()
            );
            _logger.LogInformation("GetFacilityServiceDetails result: {Result}", result);
            return result;
        }

        public const string ToolName = "GetFacilityServiceDetails";
        public const string ToolDescription = "Retrieve detailed information about a specific service at a facility.";
        public static readonly IReadOnlyList<McpToolProperty> Properties = new List<McpToolProperty>
        {
            new McpToolProperty {
                Name = "facilityId",
                Type = "string",
                Description = "The unique identifier of the facility, e.g., 'vha_688'."
            },
            new McpToolProperty {
                Name = "serviceId",
                Type = "string",
                Description = "The unique identifier for the specific service, e.g., 'covid19Vaccine'."
            }
        };
    }

    // POCO for Azure Function argument binding
    public class GetFacilityServiceDetailsRequest
    {
        public string facilityId { get; set; } = string.Empty;
        public string serviceId { get; set; } = string.Empty;
    }
}
