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
            _mapsKey = config["Maps__Key"] ?? throw new InvalidOperationException("Maps:Key configuration is required");
            _routeBase = config["Maps__RouteBase"] ?? "https://atlas.microsoft.com";
        }

        public async Task<RouteResult> GetRouteAsync(Coordinate origin, Coordinate destination, List<AvoidRectangle> avoidAreas, DateTime? departAt = null)
        {
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
                    var avoidAreasStr = string.Join("|", avoidAreas.Take(10).Select(r => 
                        $"{r.MinLat},{r.MinLon}:{r.MaxLat},{r.MaxLon}"));
                    queryParams.Add($"avoid=avoidAreas&avoidAreas={HttpUtility.UrlEncode(avoidAreasStr)}");
                }

                // Add departure time if specified
                if (departAt.HasValue)
                {
                    queryParams.Add($"departAt={departAt.Value:yyyy-MM-ddTHH:mm:ssZ}");
                }

                var requestUrl = $"{_routeBase}/route/directions/json?{string.Join("&", queryParams)}";
                
                _logger.LogInformation("Calling Azure Maps route API with {AvoidCount} avoid areas", avoidAreas.Count);
                
                var response = await _httpClient.GetAsync(requestUrl);
                
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Azure Maps API returned error {StatusCode}: {Error}", response.StatusCode, errorContent);
                    throw new HttpRequestException($"Azure Maps API error: {response.StatusCode}");
                }

                var jsonResponse = await response.Content.ReadAsStringAsync();
                var routeResponse = ParseAzureMapsResponse(jsonResponse);
                
                _logger.LogInformation("Route calculated: {Distance}m, {Time}s", 
                    routeResponse.DistanceMeters, routeResponse.TravelTimeSeconds);
                
                return routeResponse;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling Azure Maps routing API");
                throw;
            }
        }

        private RouteResult ParseAzureMapsResponse(string jsonResponse)
        {
            using var doc = JsonDocument.Parse(jsonResponse);
            var root = doc.RootElement;

            if (!root.TryGetProperty("routes", out var routes) || routes.GetArrayLength() == 0)
            {
                throw new InvalidOperationException("No routes found in Azure Maps response");
            }

            var firstRoute = routes[0];
            var summary = firstRoute.GetProperty("summary");
            
            var distanceMeters = summary.GetProperty("lengthInMeters").GetInt32();
            var travelTimeSeconds = summary.GetProperty("travelTimeInSeconds").GetInt32();

            // Extract the route geometry as GeoJSON LineString
            var legs = firstRoute.GetProperty("legs");
            var points = new List<double[]>();
            
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
    }
}