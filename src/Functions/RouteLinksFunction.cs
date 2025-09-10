using System.Text.Json;
using Azure.Storage.Blobs;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using EmergencyManagementMCP.Models;
using System.Net;
using System.Text;
using Azure.Identity;

namespace EmergencyManagementMCP.Functions
{
    public class RouteLinksFunction
    {
        private readonly ILogger<RouteLinksFunction> _logger;
        private readonly IConfiguration _configuration;
        private readonly DefaultAzureCredential _credential;

        public RouteLinksFunction(ILogger<RouteLinksFunction> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
            _credential = new DefaultAzureCredential();
        }

        [Function("CreateRouteLink")]
        public async Task<HttpResponseData> CreateRouteLink(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "routeLinks")] HttpRequestData req)
        {
            var requestId = Guid.NewGuid().ToString("N")[..8];
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            _logger.LogInformation("Creating route link, requestId={RequestId}", requestId);

            try
            {
                // Read and parse the route specification
                string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                if (string.IsNullOrEmpty(requestBody))
                {
                    _logger.LogWarning("Empty request body for route link creation, requestId={RequestId}", requestId);
                    var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badRequest.WriteAsJsonAsync(new { error = "Request body is required", requestId });
                    return badRequest;
                }

                var routeSpec = JsonSerializer.Deserialize<RouteSpec>(requestBody);
                if (routeSpec == null || routeSpec.Features == null || routeSpec.Features.Length < 2)
                {
                    _logger.LogWarning("Invalid route specification, requestId={RequestId}", requestId);
                    var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badRequest.WriteAsJsonAsync(new { error = "Invalid route specification", requestId });
                    return badRequest;
                }

                // Generate unique ID for the route link
                var linkId = Guid.NewGuid().ToString("N")[..12]; // Shorter ID for friendlier URLs

                // Get storage account connection
                var storageUrl = _configuration["Storage__BlobServiceUrl"];
                if (string.IsNullOrEmpty(storageUrl))
                {
                    throw new InvalidOperationException("Storage__BlobServiceUrl configuration is missing");
                }

                var blobServiceClient = new BlobServiceClient(new Uri(storageUrl), _credential);
                var containerClient = blobServiceClient.GetBlobContainerClient("links");
                
                // Ensure container exists
                await containerClient.CreateIfNotExistsAsync();

                // Store route specification as JSON blob
                var blobName = $"{linkId}.json";
                var blobClient = containerClient.GetBlobClient(blobName);

                var routeSpecJson = JsonSerializer.Serialize(routeSpec, new JsonSerializerOptions 
                { 
                    WriteIndented = false 
                });

                using var stream = new MemoryStream(Encoding.UTF8.GetBytes(routeSpecJson));
                await blobClient.UploadAsync(stream, overwrite: true);

                // Set TTL metadata if specified
                if (routeSpec.TtlMinutes.HasValue)
                {
                    var expiresAt = DateTime.UtcNow.AddMinutes(routeSpec.TtlMinutes.Value);
                    var metadata = new Dictionary<string, string>
                    {
                        ["ExpiresAt"] = expiresAt.ToString("O")
                    };
                    await blobClient.SetMetadataAsync(metadata);
                }

                stopwatch.Stop();

                // Determine public base URL for viewing links.
                // Priority: explicit config (RouteLinks:BaseUrl or RouteLinks__BaseUrl) -> WEBSITE_HOSTNAME -> request host.
                var configuredBase = _configuration["RouteLinks:BaseUrl"] ?? _configuration["RouteLinks__BaseUrl"]; // support both naming styles
                var websiteHost = Environment.GetEnvironmentVariable("WEBSITE_HOSTNAME");
                string viewBase;
                if (!string.IsNullOrWhiteSpace(configuredBase))
                {
                    viewBase = configuredBase.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? configuredBase : $"https://{configuredBase}";
                }
                else if (!string.IsNullOrWhiteSpace(websiteHost))
                {
                    viewBase = $"https://{websiteHost}";
                }
                else
                {
                    // Fall back to request URL (likely local dev)
                    viewBase = req.Url.Scheme + "://" + req.Url.Host + ((req.Url.Port != 80 && req.Url.Port != 443) ? ":" + req.Url.Port : string.Empty);
                }
                viewBase = viewBase.TrimEnd('/');

                var routeLink = new RouteLink
                {
                    Id = linkId,
                    Url = $"{viewBase}/view?id={linkId}",
                    CreatedAt = DateTime.UtcNow,
                    ExpiresAt = routeSpec.TtlMinutes.HasValue ? DateTime.UtcNow.AddMinutes(routeSpec.TtlMinutes.Value) : null
                };

                _logger.LogInformation("Route link created successfully: {LinkId}, requestId={RequestId}, elapsed={ElapsedMs}ms",
                    linkId, requestId, stopwatch.ElapsedMilliseconds);

                var response = req.CreateResponse(HttpStatusCode.Created);
                response.Headers.Add("Content-Type", "application/json");
                await response.WriteAsJsonAsync(routeLink);

                return response;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "Failed to create route link, requestId={RequestId}, elapsed={ElapsedMs}ms",
                    requestId, stopwatch.ElapsedMilliseconds);

                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteAsJsonAsync(new 
                { 
                    error = "Failed to create route link",
                    message = ex.Message,
                    requestId
                });

                return errorResponse;
            }
        }

        [Function("GetRouteLink")]
        public async Task<HttpResponseData> GetRouteLink(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "routeLinks/{id}")] HttpRequestData req,
            string id)
        {
            var requestId = Guid.NewGuid().ToString("N")[..8];
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            _logger.LogInformation("Retrieving route link: {LinkId}, requestId={RequestId}", id, requestId);

            try
            {
                if (string.IsNullOrEmpty(id))
                {
                    var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badRequest.WriteAsJsonAsync(new { error = "Route link ID is required", requestId });
                    return badRequest;
                }

                // Get storage account connection
                var storageUrl = _configuration["Storage__BlobServiceUrl"];
                if (string.IsNullOrEmpty(storageUrl))
                {
                    throw new InvalidOperationException("Storage__BlobServiceUrl configuration is missing");
                }

                var blobServiceClient = new BlobServiceClient(new Uri(storageUrl), _credential);
                var containerClient = blobServiceClient.GetBlobContainerClient("links");
                var blobClient = containerClient.GetBlobClient($"{id}.json");

                // Check if blob exists
                var exists = await blobClient.ExistsAsync();
                if (!exists.Value)
                {
                    _logger.LogWarning("Route link not found: {LinkId}, requestId={RequestId}", id, requestId);
                    var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                    await notFound.WriteAsJsonAsync(new { error = "Route link not found", requestId });
                    return notFound;
                }

                // Check TTL if metadata exists
                var properties = await blobClient.GetPropertiesAsync();
                if (properties.Value.Metadata.TryGetValue("ExpiresAt", out var expiresAtString))
                {
                    if (DateTime.TryParse(expiresAtString, out var expiresAt) && expiresAt < DateTime.UtcNow)
                    {
                        _logger.LogWarning("Route link expired: {LinkId}, expiredAt={ExpiresAt}, requestId={RequestId}", 
                            id, expiresAt, requestId);
                        var gone = req.CreateResponse(HttpStatusCode.Gone);
                        await gone.WriteAsJsonAsync(new { error = "Route link has expired", requestId });
                        return gone;
                    }
                }

                // Download and return the route specification
                var downloadResult = await blobClient.DownloadContentAsync();
                var routeSpecJson = downloadResult.Value.Content.ToString();

                stopwatch.Stop();

                _logger.LogInformation("Route link retrieved successfully: {LinkId}, requestId={RequestId}, elapsed={ElapsedMs}ms",
                    id, requestId, stopwatch.ElapsedMilliseconds);

                var response = req.CreateResponse(HttpStatusCode.OK);
                response.Headers.Add("Content-Type", "application/json");
                await response.WriteStringAsync(routeSpecJson);

                return response;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "Failed to retrieve route link: {LinkId}, requestId={RequestId}, elapsed={ElapsedMs}ms",
                    id, requestId, stopwatch.ElapsedMilliseconds);

                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteAsJsonAsync(new 
                { 
                    error = "Failed to retrieve route link",
                    message = ex.Message,
                    requestId
                });

                return errorResponse;
            }
        }
    }
}