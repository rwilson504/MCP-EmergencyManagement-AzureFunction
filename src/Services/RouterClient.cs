using EmergencyManagementMCP.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System.Text.Json;
using System.Web;

namespace EmergencyManagementMCP.Services
{
    public class RouterClient : IRouterClient
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<RouterClient> _logger;
        private readonly string _mapsKey;
        private readonly string _routeBase;

        public RouterClient(HttpClient httpClient, ILogger<RouterClient> logger, IConfiguration config)
        {
            _httpClient = httpClient;
            _logger = logger;
            _mapsKey = config["Maps:Key"] ?? throw new InvalidOperationException("Maps:Key configuration is required");
            _routeBase = config["Maps:RouteBase"] ?? "https://atlas.microsoft.com";
            
            _logger.LogInformation("RouterClient initialized with Maps API base: {RouteBase}", _routeBase);
            
            // Don't log the actual API key for security, just indicate if it's present
            _logger.LogDebug("Maps API key configured: {HasKey}", !string.IsNullOrEmpty(_mapsKey));
        }

        public async Task<RouteResult> GetRouteAsync(Coordinate origin, Coordinate destination, List<AvoidRectangle> avoidAreas, DateTime? departAt = null)
        {
            var requestId = Guid.NewGuid().ToString("N")[..8];
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            _logger.LogDebug("Starting route calculation: origin=[{OriginLat},{OriginLon}], destination=[{DestLat},{DestLon}], avoids={AvoidCount}, departAt={DepartAt}, requestId={RequestId}",
                origin.Lat, origin.Lon, destination.Lat, destination.Lon, avoidAreas.Count, departAt, requestId);

            try
            {
                var queryParams = new List<string>
                {
                    $"api-version=1.0",
                    $"subscription-key={_mapsKey}",
                    $"query={origin.Lat},{origin.Lon}:{destination.Lat},{destination.Lon}",
                    "routeType=fastest",
                    "travelMode=car"
                };

                // Add avoid areas if any (limit to 10 as per Azure Maps)
                if (avoidAreas.Any())
                {
                    var areasToUse = avoidAreas.Take(10).ToList();
                    var avoidAreasStr = string.Join("|", areasToUse.Select(r => 
                        $"{r.MinLat},{r.MinLon}:{r.MaxLat},{r.MaxLon}"));
                    queryParams.Add($"avoid=avoidAreas&avoidAreas={HttpUtility.UrlEncode(avoidAreasStr)}");
                    
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
                
                _logger.LogDebug("Making Azure Maps API request, requestId={RequestId}", requestId);
                // Don't log the full URL as it contains the API key
                
                var httpStopwatch = System.Diagnostics.Stopwatch.StartNew();
                var response = await _httpClient.GetAsync(requestUrl);
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

                return new RouteResult
                {
                    DistanceMeters = distanceMeters,
                    TravelTimeSeconds = travelTimeSeconds,
                    PolylineGeoJson = polylineGeoJson
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