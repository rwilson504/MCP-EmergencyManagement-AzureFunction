using System.Text.Json;
using Azure.Storage.Blobs;
using Azure.Storage.Sas;
using Azure.Core;
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
    private readonly TokenCredential _credential;

        public RouteLinksFunction(ILogger<RouteLinksFunction> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;

            // Prefer an explicitly configured user-assigned managed identity client id if provided.
            var userAssignedClientId = _configuration["ManagedIdentity:ClientId"]
                                        ?? Environment.GetEnvironmentVariable("ManagedIdentity:ClientId");

            if (!string.IsNullOrWhiteSpace(userAssignedClientId))
            {
                _credential = new ManagedIdentityCredential(userAssignedClientId);
                _logger.LogInformation("RouteLinksFunction using user-assigned managed identity clientId={ClientId}", userAssignedClientId);
            }
            else
            {
                // Fall back to default chain (will use system-assigned if available)
                _credential = new DefaultAzureCredential();
                _logger.LogInformation("RouteLinksFunction using DefaultAzureCredential (no explicit user-assigned client id provided)");
            }

            // Proactive diagnostic: attempt a lightweight token acquisition for Storage scope (non-fatal if it fails)
            _ = Task.Run(async () =>
            {
                try
                {
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    var token = await _credential.GetTokenAsync(new TokenRequestContext(new[] { "https://storage.azure.com/.default" }), cts.Token);
                    _logger.LogDebug("Managed identity token acquired for storage. ExpiresOn={ExpiresOn:O}", token.ExpiresOn);
                }
                catch (Exception diagEx)
                {
                    _logger.LogWarning(diagEx, "Initial managed identity token acquisition failed during function startup. This may be transient right after deployment.");
                }
            });
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
                var storageUrl = _configuration["Storage:BlobServiceUrl"];
                if (string.IsNullOrEmpty(storageUrl))
                {
                    throw new InvalidOperationException("Storage:BlobServiceUrl configuration is missing");
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
                // Priority: explicit config (RouteLinks:BaseUrl or RouteLinks:BaseUrl) request host.
                var configuredBase = _configuration["RouteLinks:BaseUrl"] ?? _configuration["RouteLinks:BaseUrl"]; // support both naming styles                
                string viewBase;
                if (!string.IsNullOrWhiteSpace(configuredBase))
                {
                    viewBase = configuredBase.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? configuredBase : $"https://{configuredBase}";
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

        [Function("GetRouteLinkPublic")]
        public async Task<HttpResponseData> GetRouteLinkPublic(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "public/routeLinks/{id}")] HttpRequestData req,
            string id)
        {
            var requestId = Guid.NewGuid().ToString("N")[..8];
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            _logger.LogInformation("Public route link access: {LinkId}, requestId={RequestId}, origin={Origin}, referer={Referer}", 
                id, requestId, 
                req.Headers.TryGetValues("Origin", out var origins) ? string.Join(",", origins) : "none",
                req.Headers.TryGetValues("Referer", out var referers) ? string.Join(",", referers) : "none");

            try
            {
                if (string.IsNullOrEmpty(id))
                {
                    var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badRequest.WriteAsJsonAsync(new { error = "Route link ID is required", requestId });
                    return badRequest;
                }

                // Basic security: validate referrer if present (not foolproof but adds a layer)
                if (req.Headers.TryGetValues("Referer", out var refererValues))
                {
                    var referer = refererValues.FirstOrDefault();
                    if (!string.IsNullOrEmpty(referer))
                    {
                        Uri? refererUri = null;
                        try
                        {
                            refererUri = new Uri(referer);
                        }
                        catch (Exception uriEx)
                        {
                            _logger.LogWarning(uriEx, "Referer parse failed: raw={RawReferer}, linkId={LinkId}, requestId={RequestId}", referer, id, requestId);
                        }

                        // Retrieve configured base (supports full URL or hostname). Prefer configuration binding over raw env to allow standard __ mapping.
                        var rawConfigured = _configuration["RouteLinks:BaseUrl"] ?? Environment.GetEnvironmentVariable("RouteLinks:BaseUrl");
                        var hostExtractions = new List<string>();
                        if (!string.IsNullOrWhiteSpace(rawConfigured))
                        {
                            // Support comma/semicolon separated list if user provides multiple
                            var parts = rawConfigured.Split(new[]{',',';'}, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                            foreach (var part in parts)
                            {
                                var candidate = part.Trim();
                                if (candidate.Length == 0) continue;

                                // If missing scheme but contains a slash, assume https:// for parsing
                                string toParse = candidate;
                                if (!candidate.Contains("://", StringComparison.Ordinal) && candidate.Contains('/'))
                                {
                                    toParse = "https://" + candidate;
                                }

                                if (Uri.TryCreate(toParse, UriKind.Absolute, out var parsedUri))
                                {
                                    hostExtractions.Add(parsedUri.Host.ToLowerInvariant());
                                }
                                else
                                {
                                    // Strip any trailing path if present (e.g. host/path)
                                    var slashIndex = candidate.IndexOf('/');
                                    var hostOnly = (slashIndex > 0 ? candidate[..slashIndex] : candidate).ToLowerInvariant();
                                    // Remove port if supplied (host:port)
                                    var colonIndex = hostOnly.IndexOf(':');
                                    if (colonIndex > -1)
                                    {
                                        hostOnly = hostOnly[..colonIndex];
                                    }
                                    hostExtractions.Add(hostOnly);
                                }
                            }
                        }

                        var allowedHosts = new[]{"localhost","127.0.0.1"}
                            .Concat(hostExtractions)
                            .Where(h => !string.IsNullOrWhiteSpace(h))
                            .Select(h => h!)
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .ToArray();

                        _logger.LogDebug("Referer host extraction: rawConfigured={RawConfigured}, extractedHosts={Extracted}", rawConfigured, string.Join(',', hostExtractions));

                        if (refererUri != null)
                        {
                            // Per-host evaluation logging
                            foreach (var host in allowedHosts)
                            {
                                var hostMatch = refererUri.Host.Equals(host, StringComparison.OrdinalIgnoreCase) ||
                                                refererUri.Host.EndsWith($".{host}", StringComparison.OrdinalIgnoreCase);
                                _logger.LogDebug("Referer check detail: linkId={LinkId}, requestId={RequestId}, refererHost={RefererHost}, candidateHost={Candidate}, matched={Matched}", id, requestId, refererUri.Host, host, hostMatch);
                            }

                            var isValidReferer = allowedHosts.Any(host =>
                                refererUri.Host.Equals(host, StringComparison.OrdinalIgnoreCase) ||
                                refererUri.Host.EndsWith($".{host}", StringComparison.OrdinalIgnoreCase));

                            _logger.LogDebug("Referer validation summary: linkId={LinkId}, requestId={RequestId}, referer={Referer}, host={RefererHost}, allowedHosts={Allowed}, valid={Valid}",
                                id, requestId, referer, refererUri.Host, string.Join(',', allowedHosts), isValidReferer);

                            if (!isValidReferer)
                            {
                                _logger.LogWarning("Invalid referer for public route link access: {LinkId}, referer={Referer}, allowedHosts={Allowed}, requestId={RequestId}",
                                    id, referer, string.Join(',', allowedHosts), requestId);
                                var forbidden = req.CreateResponse(HttpStatusCode.Forbidden);
                                await forbidden.WriteAsJsonAsync(new { error = "Access not allowed from this origin", requestId });
                                return forbidden;
                            }
                        }
                    }
                    else
                    {
                        _logger.LogDebug("Referer header was present but empty/null: linkId={LinkId}, requestId={RequestId}", id, requestId);
                    }
                }

                // Get storage account connection
                var storageUrl = _configuration["Storage:BlobServiceUrl"];
                if (string.IsNullOrEmpty(storageUrl))
                {
                    throw new InvalidOperationException("Storage:BlobServiceUrl configuration is missing");
                }

                var blobServiceClient = new BlobServiceClient(new Uri(storageUrl), _credential);
                var containerClient = blobServiceClient.GetBlobContainerClient("links");
                var blobClient = containerClient.GetBlobClient($"{id}.json");

                // Check if blob exists
                var exists = await blobClient.ExistsAsync();
                if (!exists.Value)
                {
                    _logger.LogWarning("Public route link not found: {LinkId}, requestId={RequestId}", id, requestId);
                    var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                    await notFound.WriteAsJsonAsync(new { error = "Route link not found", requestId });
                    return notFound;
                }

                // Check TTL if metadata exists
                var properties = await blobClient.GetPropertiesAsync();
                DateTime? expiresAt = null;
                if (properties.Value.Metadata.TryGetValue("ExpiresAt", out var expiresAtString))
                {
                    if (DateTime.TryParse(expiresAtString, out var parsedExpiresAt))
                    {
                        expiresAt = parsedExpiresAt;
                        if (parsedExpiresAt < DateTime.UtcNow)
                        {
                            _logger.LogWarning("Public route link expired: {LinkId}, expiredAt={ExpiresAt}, requestId={RequestId}", 
                                id, parsedExpiresAt, requestId);
                            var gone = req.CreateResponse(HttpStatusCode.Gone);
                            await gone.WriteAsJsonAsync(new { error = "Route link has expired", requestId });
                            return gone;
                        }
                    }
                }

                // Download and parse the route specification to create Azure Maps post data
                var downloadResult = await blobClient.DownloadContentAsync();
                var routeSpecJson = downloadResult.Value.Content.ToString();
                var routeSpec = JsonSerializer.Deserialize<RouteSpec>(routeSpecJson);

                // Create Azure Maps compatible post data (without ttlMinutes)
                AzureMapsPostData? azureMapsPostData = null;
                if (routeSpec != null)
                {
                    azureMapsPostData = new AzureMapsPostData
                    {
                        Type = routeSpec.Type,
                        Features = routeSpec.Features,
                        AvoidAreas = routeSpec.AvoidAreas,
                        RouteOutputOptions = routeSpec.RouteOutputOptions,
                        TravelMode = routeSpec.TravelMode
                    };
                }

                stopwatch.Stop();
                _logger.LogInformation("Public route link retrieved successfully: {LinkId}, requestId={RequestId}, elapsed={ElapsedMs}ms",
                    id, requestId, stopwatch.ElapsedMilliseconds);

                var response = req.CreateResponse(HttpStatusCode.OK);                
                // Add CORS headers for browser access (with detailed diagnostics)
                if (req.Headers.TryGetValues("Origin", out var originValues))
                {
                    var origin = originValues.FirstOrDefault();
                    if (string.IsNullOrEmpty(origin))
                    {
                        _logger.LogDebug("CORS: Origin header present but empty, linkId={LinkId}, requestId={RequestId}", id, requestId);
                    }
                    else
                    {
                        var allowedOriginsList = new List<string>
                        {
                            "http://localhost:3000",
                            "https://localhost:3000",
                            "http://127.0.0.1:3000",
                            "https://127.0.0.1:3000"
                        };

                        // NOTE: Previously this used Environment.GetEnvironmentVariable("RouteLinks:BaseUrl").
                        // Environment variables rarely use colon separators; prefer configuration.
                        var configuredBase = _configuration["RouteLinks:BaseUrl"]; // e.g. doem-app-<suffix>.azurewebsites.net or full https URL
                        if (!string.IsNullOrWhiteSpace(configuredBase))
                        {
                            // Normalize to full URLs and add both http/https variants for flexibility
                            var full = configuredBase.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? configuredBase : $"https://{configuredBase}";
                            if (Uri.TryCreate(full, UriKind.Absolute, out var baseUri))
                            {
                                allowedOriginsList.Add($"https://{baseUri.Host}");
                                allowedOriginsList.Add($"http://{baseUri.Host}");
                            }
                            else
                            {
                                _logger.LogWarning("CORS: Configured RouteLinks:BaseUrl value invalid: {ConfiguredBase}, linkId={LinkId}, requestId={RequestId}", configuredBase, id, requestId);
                            }
                        }

                        var allowedOrigins = allowedOriginsList.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
                        var isAllowed = allowedOrigins.Contains(origin, StringComparer.OrdinalIgnoreCase);

                        _logger.LogDebug("CORS: Evaluating origin={Origin}; allowedOrigins={Allowed}; allowedMatch={Match}; linkId={LinkId}, requestId={RequestId}",
                            origin, string.Join(',', allowedOrigins), isAllowed, id, requestId);

                        if (isAllowed)
                        {
                            response.Headers.Add("Access-Control-Allow-Origin", origin);
                            response.Headers.Add("Access-Control-Allow-Credentials", "false");
                            _logger.LogDebug("CORS: Applied Access-Control-Allow-Origin for {Origin}, linkId={LinkId}, requestId={RequestId}", origin, id, requestId);
                        }
                        else
                        {
                            _logger.LogInformation("CORS: Origin not in allow list, no header set. origin={Origin}; linkId={LinkId}; requestId={RequestId}", origin, id, requestId);
                        }
                    }
                }
                else
                {
                    _logger.LogDebug("CORS: No Origin header present on request, linkId={LinkId}, requestId={RequestId}", id, requestId);
                }
                
                await response.WriteAsJsonAsync(azureMapsPostData);
                return response;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "Failed to retrieve public route link: {LinkId}, requestId={RequestId}, elapsed={ElapsedMs}ms",
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
                var storageUrl = _configuration["Storage:BlobServiceUrl"];
                if (string.IsNullOrEmpty(storageUrl))
                {
                    throw new InvalidOperationException("Storage:BlobServiceUrl configuration is missing");
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
                DateTime? expiresAt = null;
                if (properties.Value.Metadata.TryGetValue("ExpiresAt", out var expiresAtString))
                {
                    if (DateTime.TryParse(expiresAtString, out var parsedExpiresAt))
                    {
                        expiresAt = parsedExpiresAt;
                        if (parsedExpiresAt < DateTime.UtcNow)
                        {
                            _logger.LogWarning("Route link expired: {LinkId}, expiredAt={ExpiresAt}, requestId={RequestId}", 
                                id, parsedExpiresAt, requestId);
                            var gone = req.CreateResponse(HttpStatusCode.Gone);
                            await gone.WriteAsJsonAsync(new { error = "Route link has expired", requestId });
                            return gone;
                        }
                    }
                }

                // Download and parse the route specification to create Azure Maps post data
                var downloadResult = await blobClient.DownloadContentAsync();
                var routeSpecJson = downloadResult.Value.Content.ToString();
                var routeSpec = JsonSerializer.Deserialize<RouteSpec>(routeSpecJson);

                // Create Azure Maps compatible post data (without ttlMinutes)
                AzureMapsPostData? azureMapsPostData = null;
                if (routeSpec != null)
                {
                    azureMapsPostData = new AzureMapsPostData
                    {
                        Type = routeSpec.Type,
                        Features = routeSpec.Features,
                        AvoidAreas = routeSpec.AvoidAreas,
                        RouteOutputOptions = routeSpec.RouteOutputOptions,
                        TravelMode = routeSpec.TravelMode
                    };
                }

                // Return information about the public endpoint for this route link
                var routeLinkData = new RouteLinkData
                {
                    Id = id,
                    SasUrl = $"/api/public/routeLinks/{id}", // Point to the public endpoint
                    CreatedAt = properties.Value.CreatedOn.DateTime,
                    ExpiresAt = expiresAt,
                    AzureMapsPostData = azureMapsPostData
                };

                stopwatch.Stop();
                _logger.LogInformation("Route link info retrieved successfully: {LinkId}, requestId={RequestId}, elapsed={ElapsedMs}ms",
                    id, requestId, stopwatch.ElapsedMilliseconds);

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(routeLinkData);

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