using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Azure.Identity;
using Azure.Core;
using System.Text.Json;
using System.Net;
using System;

namespace EmergencyManagementMCP.Functions
{
    public class MapsTokenFunction
    {
        private readonly ILogger<MapsTokenFunction> _logger;
        private readonly DefaultAzureCredential _credential;

        public MapsTokenFunction(ILogger<MapsTokenFunction> logger)
        {
            _logger = logger;
            _credential = new DefaultAzureCredential();
        }

        [Function("GetMapsToken")]
        public async Task<HttpResponseData> GetMapsToken(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "maps-token")] HttpRequestData req)
        {
            var requestId = Guid.NewGuid().ToString("N")[..8];
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            _logger.LogInformation("Maps token request from {RemoteIpAddress}, requestId={RequestId}",
                req.Url.Host, requestId);

            try
            {
                // Request Azure Maps access token using managed identity
                var tokenContext = new TokenRequestContext(new[] { "https://atlas.microsoft.com/.default" });
                var tokenResult = await _credential.GetTokenAsync(tokenContext, default);

                stopwatch.Stop();

                _logger.LogDebug("Azure Maps token acquired successfully, expires: {ExpiresOn}, requestId={RequestId}, elapsed={ElapsedMs}ms",
                    tokenResult.ExpiresOn, requestId, stopwatch.ElapsedMilliseconds);

                var response = req.CreateResponse(HttpStatusCode.OK);
                response.Headers.Add("Content-Type", "application/json");
                
                await response.WriteAsJsonAsync(new 
                { 
                    access_token = tokenResult.Token, 
                    expires_on = tokenResult.ExpiresOn.ToUnixTimeSeconds(),
                    token_type = "Bearer"
                });

                return response;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "Failed to acquire Azure Maps token, requestId={RequestId}, elapsed={ElapsedMs}ms",
                    requestId, stopwatch.ElapsedMilliseconds);

                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                errorResponse.Headers.Add("Content-Type", "application/json");
                
                await errorResponse.WriteAsJsonAsync(new 
                { 
                    error = "Failed to acquire Azure Maps token",
                    message = ex.Message,
                    requestId
                });

                return errorResponse;
            }
        }
    }
}