using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;
using Microsoft.Extensions.Logging;
using EmergencyManagementMCP.Services;
using EmergencyManagementMCP.Common;

namespace EmergencyManagementMCP.Tools
{
    public class GetFacilityByIdTool
    {
        private readonly EmergencyManagementService _service;
        private readonly ILogger<GetFacilityByIdTool> _logger;

        public GetFacilityByIdTool(EmergencyManagementService service, ILogger<GetFacilityByIdTool> logger)
        {
            _service = service;
            _logger = logger;
        }

        [Function(nameof(GetFacilityByIdTool))]
        public async Task<object> Run(
            [McpToolTrigger(ToolName, ToolDescription)] GetFacilityByIdRequest request,
            ToolInvocationContext context)
        {
            _logger.LogInformation("Received GetFacilityById request: {FacilityId}", request.facilityId);
            var parameters = McpToolParameterHelper.BuildParametersFromRequest(request, Properties);
            var result = await _service.GetFacilityByIdAsync(parameters["facilityId"]?.ToString());
            _logger.LogInformation("GetFacilityById result: {Result}", result);
            return result;
        }

        public const string ToolName = "GetFacilityById";
        public const string ToolDescription = "Retrieve detailed information about a specific VA facility by its ID.";
        public static readonly IReadOnlyList<McpToolProperty> Properties = new List<McpToolProperty>
        {
            new McpToolProperty {
                Name = "facilityId",
                Type = "string",
                Description = "The unique identifier of the facility, e.g., 'vha_688'."
            }
        };
    }

    // POCO for Azure Function argument binding
    public class GetFacilityByIdRequest
    {
        public string facilityId { get; set; } = string.Empty;
    }
}
