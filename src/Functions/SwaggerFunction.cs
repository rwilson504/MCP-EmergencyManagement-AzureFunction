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
            var requestId = Guid.NewGuid().ToString("N")[..8];
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            _logger.LogInformation("Serving Swagger JSON request from {RemoteIpAddress}, requestId={RequestId}", 
                req.Url.Host, requestId);

            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "swagger.json");
            _logger.LogDebug("Looking for Swagger file at: {FilePath}, requestId={RequestId}", filePath, requestId);

            var response = req.CreateResponse();
            response.Headers.Add("Content-Type", "application/json");

            if (!File.Exists(filePath))
            {
                stopwatch.Stop();
                _logger.LogError("Swagger file not found at path: {FilePath}, elapsed={ElapsedMs}ms, requestId={RequestId}", 
                    filePath, stopwatch.ElapsedMilliseconds, requestId);
                    
                response.StatusCode = HttpStatusCode.NotFound;
                var errorJson = JsonSerializer.Serialize(new { error = "Swagger file not found.", requestId });
                await response.WriteStringAsync(errorJson);
                return response;
            }

            try
            {
                var json = await File.ReadAllTextAsync(filePath);
                _logger.LogDebug("Swagger file loaded: {Size} chars, requestId={RequestId}", json.Length, requestId);

                // Parse the JSON and replace the "host" field
                var swaggerDoc = JsonNode.Parse(json);
                if (swaggerDoc != null && swaggerDoc["host"] != null)
                {
                    var host = req.Url.Host;
                    var port = req.Url.Port;
                    var originalHost = swaggerDoc["host"]?.ToString();

                    // Include the port if it's not a standard port (80 for HTTP, 443 for HTTPS)
                    if (!(req.Url.Scheme == "http" && port == 80) && !(req.Url.Scheme == "https" && port == 443))
                    {
                        host = $"{host}:{port}";
                    }

                    swaggerDoc["host"] = host;
                    
                    _logger.LogDebug("Updated Swagger host from '{OriginalHost}' to '{NewHost}', requestId={RequestId}", 
                        originalHost, host, requestId);
                }

                stopwatch.Stop();
                response.StatusCode = HttpStatusCode.OK;
                await response.WriteStringAsync(swaggerDoc?.ToJsonString() ?? json);
                
                _logger.LogInformation("Swagger JSON served successfully: {Size} chars, elapsed={ElapsedMs}ms, requestId={RequestId}", 
                    json.Length, stopwatch.ElapsedMilliseconds, requestId);
                    
                return response;
            }
            catch (JsonException jsonEx)
            {
                stopwatch.Stop();
                _logger.LogError(jsonEx, "Failed to parse Swagger JSON file, elapsed={ElapsedMs}ms, requestId={RequestId}", 
                    stopwatch.ElapsedMilliseconds, requestId);
                    
                response.StatusCode = HttpStatusCode.InternalServerError;
                var errorJson = JsonSerializer.Serialize(new { error = "Invalid Swagger JSON format.", requestId });
                await response.WriteStringAsync(errorJson);
                return response;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "Unexpected error serving Swagger JSON, elapsed={ElapsedMs}ms, requestId={RequestId}", 
                    stopwatch.ElapsedMilliseconds, requestId);
                    
                response.StatusCode = HttpStatusCode.InternalServerError;
                var errorJson = JsonSerializer.Serialize(new { error = "Internal server error.", requestId });
                await response.WriteStringAsync(errorJson);
                return response;
            }
        }
    }
}