using System.IO;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace EmergencyManagementMCP.Functions
{
    public class SwaggerFunction
    {
        private readonly ILogger<SwaggerFunction> _logger;

        public SwaggerFunction(ILogger<SwaggerFunction> logger)
        {
            _logger = logger;
        }

        [Function("SwaggerFunction")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "swagger.json")] HttpRequestData req)
        {
            _logger.LogInformation("Serving the Swagger file.");

            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "swagger.json");

            var response = req.CreateResponse();
            response.Headers.Add("Content-Type", "application/json");
            //response.Headers.Add("Content-Disposition", "attachment; filename=swagger.json");

            if (!File.Exists(filePath))
            {
                response.StatusCode = HttpStatusCode.NotFound;
                var errorJson = JsonSerializer.Serialize(new { error = "Swagger file not found." });
                await response.WriteStringAsync(errorJson);
                return response;
            }

            var json = await File.ReadAllTextAsync(filePath);

            // Parse the JSON and replace the "host" field
            var swaggerDoc = JsonNode.Parse(json);
            if (swaggerDoc != null && swaggerDoc["host"] != null)
            {
                var host = req.Url.Host;
                var port = req.Url.Port;

                // Include the port if it's not a standard port (80 for HTTP, 443 for HTTPS)
                if (!(req.Url.Scheme == "http" && port == 80) && !(req.Url.Scheme == "https" && port == 443))
                {
                    host = $"{host}:{port}";
                }

                swaggerDoc["host"] = host;
            }

            response.StatusCode = HttpStatusCode.OK;
            await response.WriteStringAsync(swaggerDoc?.ToJsonString() ?? json);
            return response;
        }
    }
}