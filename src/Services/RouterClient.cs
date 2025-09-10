using EmergencyManagementMCP.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System.Text.Json;
using System.Web;
using Azure.Core;
using Azure.Identity;

namespace EmergencyManagementMCP.Services
{
    public class RouterClient : IRouterClient
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<RouterClient> _logger;
        private readonly TokenCredential _credential;
        private readonly string _routeBase;
        private readonly string? _mapsClientId; // Azure Maps account client ID for x-ms-client-id header

        public RouterClient(HttpClient httpClient, ILogger<RouterClient> logger, IConfiguration config)
        {
            _httpClient = httpClient;
            _logger = logger;
            _routeBase = config["Maps:RouteBase"] ?? "https://atlas.microsoft.com";
            _mapsClientId = config["Maps:ClientId"];
            
            // Use ManagedIdentityCredential with specific client ID for Azure Functions
            var managedIdentityClientId = config["ManagedIdentity:ClientId"]
                ?? config["ManagedIdentity:ClientId"]
                ?? config["AzureWebJobsStorage:clientId"]; // backward compatibility
            if (!string.IsNullOrEmpty(managedIdentityClientId))
            {
                _logger.LogDebug("Using ManagedIdentityCredential with client ID: {ClientId}", managedIdentityClientId);
                _credential = new ManagedIdentityCredential(managedIdentityClientId);
            }
            else
            {
                _logger.LogDebug("Using DefaultAzureCredential (no client ID specified)");
                _credential = new DefaultAzureCredential();
            }
            
            _logger.LogInformation("RouterClient initialized with Maps API base: {RouteBase}, credential: {CredentialType}", 
                _routeBase, _credential.GetType().Name);
        }

        public async Task<RouteResult> GetRouteAsync(Coordinate origin, Coordinate destination, List<AvoidRectangle> avoidAreas, DateTime? departAt = null)
        {
            var requestId = Guid.NewGuid().ToString("N")[..8];
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            _logger.LogDebug("Starting route calculation: origin=[{OriginLat},{OriginLon}], destination=[{DestLat},{DestLon}], avoids={AvoidCount}, departAt={DepartAt}, requestId={RequestId}",
                origin.Lat, origin.Lon, destination.Lat, destination.Lon, avoidAreas.Count, departAt, requestId);

            try
            {
                // Get access token for Azure Maps
                var tokenContext = new TokenRequestContext(new[] { "https://atlas.microsoft.com/.default" });
                var tokenResult = await _credential.GetTokenAsync(tokenContext, CancellationToken.None);
                
                _logger.LogDebug("Obtained access token for Azure Maps, expires at: {ExpiresOn}, requestId={RequestId}", 
                    tokenResult.ExpiresOn, requestId);
                                    
                var queryParams = new List<string>
                {
                    $"api-version=1.0",
                    $"query={origin.Lat},{origin.Lon}:{destination.Lat},{destination.Lon}",
                    "routeType=fastest",
                    "travelMode=car",
                    "instructionsType=text"  // Request text-based driving instructions
                };

                // Add avoid areas if any (limit to 10 as per Azure Maps)
                if (avoidAreas.Any())
                {
                    var areasToUse = avoidAreas.Take(10).ToList();
                    var avoidAreasStr = string.Join("|", areasToUse.Select(r => 
                        $"{r.MinLat},{r.MinLon}:{r.MaxLat},{r.MaxLon}"));
                    queryParams.Add($"avoidAreas={HttpUtility.UrlEncode(avoidAreasStr)}");
                    
                    _logger.LogDebug("Added {ActualCount} avoid areas (of {TotalCount} requested), requestId={RequestId}", 
                        areasToUse.Count, avoidAreas.Count, requestId);
                        
                    if (avoidAreas.Count > 10)
                    {
                        _logger.LogWarning("Truncated avoid areas from {Total} to {Used} due to Azure Maps limit, requestId={RequestId}", 
                            avoidAreas.Count, areasToUse.Count, requestId);
                    }
                }

                // Add departure time if specified
                if (departAt.HasValue)
                {
                    queryParams.Add($"departAt={departAt.Value:yyyy-MM-ddTHH:mm:ssZ}");
                    _logger.LogDebug("Added departure time: {DepartAt}, requestId={RequestId}", departAt.Value, requestId);
                }

                var requestUrl = $"{_routeBase}/route/directions/json?{string.Join("&", queryParams)}";
                
                _logger.LogDebug("Making Azure Maps API request with managed identity authentication, requestId={RequestId}", requestId);
                
                // Create HTTP request with Authorization header
                using var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tokenResult.Token);
                // Add x-ms-client-id header if Azure Maps clientId is present
                if (!string.IsNullOrEmpty(_mapsClientId))
                {
                    request.Headers.Add("x-ms-client-id", _mapsClientId);
                    _logger.LogDebug("Set x-ms-client-id header: {ClientId}", _mapsClientId);
                }
                
                var httpStopwatch = System.Diagnostics.Stopwatch.StartNew();
                var response = await _httpClient.SendAsync(request);
                httpStopwatch.Stop();
                
                _logger.LogDebug("Azure Maps API response received: status={StatusCode}, elapsed={ElapsedMs}ms, requestId={RequestId}",
                    response.StatusCode, httpStopwatch.ElapsedMilliseconds, requestId);
                
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Azure Maps API returned error {StatusCode}: {Error}, requestId={RequestId}", 
                        response.StatusCode, errorContent, requestId);
                    throw new HttpRequestException($"Azure Maps API error: {response.StatusCode} - {errorContent}");
                }

                var jsonResponse = await response.Content.ReadAsStringAsync();
                
                _logger.LogDebug("Parsing Azure Maps response: {Length} chars, requestId={RequestId}", 
                    jsonResponse.Length, requestId);
                
                var routeResponse = ParseAzureMapsResponse(jsonResponse, requestId);
                
                stopwatch.Stop();
                _logger.LogInformation("Route calculated successfully: distance={Distance}m, time={Time}s, elapsed={ElapsedMs}ms, requestId={RequestId}", 
                    routeResponse.DistanceMeters, routeResponse.TravelTimeSeconds, stopwatch.ElapsedMilliseconds, requestId);
                
                return routeResponse;
            }
            catch (HttpRequestException httpEx)
            {
                stopwatch.Stop();
                _logger.LogError(httpEx, "HTTP error calling Azure Maps routing API after {ElapsedMs}ms, requestId={RequestId}", 
                    stopwatch.ElapsedMilliseconds, requestId);
                throw;
            }
            catch (TaskCanceledException timeoutEx)
            {
                stopwatch.Stop();
                _logger.LogError(timeoutEx, "Timeout calling Azure Maps routing API after {ElapsedMs}ms, requestId={RequestId}", 
                    stopwatch.ElapsedMilliseconds, requestId);
                throw;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "Unexpected error calling Azure Maps routing API after {ElapsedMs}ms, requestId={RequestId}", 
                    stopwatch.ElapsedMilliseconds, requestId);
                throw;
            }
        }

        private RouteResult ParseAzureMapsResponse(string jsonResponse, string requestId)
        {
            _logger.LogDebug("Parsing Azure Maps response, requestId={RequestId}", requestId);
            
            try
            {
                using var doc = JsonDocument.Parse(jsonResponse);
                var root = doc.RootElement;

                if (!root.TryGetProperty("routes", out var routes) || routes.GetArrayLength() == 0)
                {
                    _logger.LogError("No routes found in Azure Maps response, requestId={RequestId}", requestId);
                    throw new InvalidOperationException("No routes found in Azure Maps response");
                }

                var firstRoute = routes[0];
                
                if (!firstRoute.TryGetProperty("summary", out var summary))
                {
                    _logger.LogError("No summary found in route response, requestId={RequestId}", requestId);
                    throw new InvalidOperationException("No summary found in route response");
                }
                
                var distanceMeters = summary.GetProperty("lengthInMeters").GetInt32();
                var travelTimeSeconds = summary.GetProperty("travelTimeInSeconds").GetInt32();

                _logger.LogDebug("Route summary parsed: distance={Distance}m, time={Time}s, requestId={RequestId}", 
                    distanceMeters, travelTimeSeconds, requestId);

                // Extract the route geometry as GeoJSON LineString
                var points = new List<double[]>();
                
                if (firstRoute.TryGetProperty("legs", out var legs))
                {
                    foreach (var leg in legs.EnumerateArray())
                    {
                        if (leg.TryGetProperty("points", out var legPoints))
                        {
                            foreach (var point in legPoints.EnumerateArray())
                            {
                                var lat = point.GetProperty("latitude").GetDouble();
                                var lon = point.GetProperty("longitude").GetDouble();
                                points.Add(new[] { lon, lat }); // GeoJSON uses [lon, lat] order
                            }
                        }
                    }
                }

                _logger.LogDebug("Extracted {PointCount} route points, requestId={RequestId}", points.Count, requestId);

                var geoJsonLineString = new
                {
                    type = "LineString",
                    coordinates = points
                };

                var polylineGeoJson = JsonSerializer.Serialize(geoJsonLineString);

                // Extract driving directions/guidance instructions
                var drivingDirections = new List<DrivingInstruction>();
                
                if (firstRoute.TryGetProperty("guidance", out var guidance) && 
                    guidance.TryGetProperty("instructions", out var instructions))
                {
                    foreach (var instruction in instructions.EnumerateArray())
                    {
                        var direction = new DrivingInstruction();
                        
                        if (instruction.TryGetProperty("routeOffsetInMeters", out var offsetElement))
                        {
                            direction.RouteOffsetInMeters = offsetElement.GetInt32();
                        }
                        
                        if (instruction.TryGetProperty("travelTimeInSeconds", out var timeElement))
                        {
                            direction.TravelTimeInSeconds = timeElement.GetInt32();
                        }
                        
                        if (instruction.TryGetProperty("message", out var messageElement))
                        {
                            direction.Message = messageElement.GetString() ?? string.Empty;
                        }
                        
                        if (instruction.TryGetProperty("point", out var pointElement))
                        {
                            direction.Point = new Coordinate
                            {
                                Lat = pointElement.GetProperty("latitude").GetDouble(),
                                Lon = pointElement.GetProperty("longitude").GetDouble()
                            };
                        }
                        
                        drivingDirections.Add(direction);
                    }
                    
                    _logger.LogDebug("Extracted {DirectionCount} driving directions, requestId={RequestId}", 
                        drivingDirections.Count, requestId);
                }
                else
                {
                    _logger.LogDebug("No driving directions found in Azure Maps response, requestId={RequestId}", requestId);
                }

                return new RouteResult
                {
                    DistanceMeters = distanceMeters,
                    TravelTimeSeconds = travelTimeSeconds,
                    //PolylineGeoJson = polylineGeoJson,
                    DrivingDirections = drivingDirections.ToArray()
                };
            }
            catch (JsonException jsonEx)
            {
                _logger.LogError(jsonEx, "Failed to parse Azure Maps JSON response, requestId={RequestId}", requestId);
                throw new InvalidOperationException("Failed to parse Azure Maps response", jsonEx);
            }
            catch (KeyNotFoundException keyEx)
            {
                _logger.LogError(keyEx, "Missing expected property in Azure Maps response, requestId={RequestId}", requestId);
                throw new InvalidOperationException("Missing expected property in Azure Maps response", keyEx);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error parsing Azure Maps response, requestId={RequestId}", requestId);
                throw;
            }
        }
    }
}