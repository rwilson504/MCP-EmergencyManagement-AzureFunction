using EmergencyManagementMCP.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System.Text.Json;
using System.Web;

namespace EmergencyManagementMCP.Services
{
    public class GeoServiceClient : IGeoServiceClient
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<GeoServiceClient> _logger;
        private readonly string _arcGisFeatureUrl;

        public GeoServiceClient(HttpClient httpClient, ILogger<GeoServiceClient> logger, IConfiguration config)
        {
            _httpClient = httpClient;
            _logger = logger;
            _arcGisFeatureUrl = config["Fires__ArcGisFeatureUrl"] ?? "https://services3.arcgis.com/T4QMspbfLg3qTGWY/ArcGIS/rest/services/WFIGS_Interagency_Perimeters_YearToDate/FeatureServer/0/query";
            
            _logger.LogInformation("GeoServiceClient initialized with ArcGIS URL: {Url}", _arcGisFeatureUrl);
        }

        public async Task<string> FetchPerimetersAsGeoJsonAsync(BoundingBox bbox, int sinceMins = 60)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var requestId = Guid.NewGuid().ToString("N")[..8];
            
            _logger.LogDebug("Starting fire perimeter fetch: bbox=[{MinLat},{MinLon},{MaxLat},{MaxLon}], since={SinceMins}min, requestId={RequestId}",
                bbox.MinLat, bbox.MinLon, bbox.MaxLat, bbox.MaxLon, sinceMins, requestId);
                
            try
            {
                // Build the WHERE clause - for fire perimeters, we might want recent fires
                // Note: The 'sinceMins' parameter can be used for temporal filtering if the service supports it
                var whereClause = "1=1"; // Default to get all features
                
                // Build geometry parameter as envelope: xmin,ymin,xmax,ymax
                var geometryParam = $"{bbox.MinLon},{bbox.MinLat},{bbox.MaxLon},{bbox.MaxLat}";
                
                // Build the query string following ArcGIS REST API standards
                var queryParams = new Dictionary<string, string>
                {
                    ["where"] = whereClause,
                    ["geometry"] = geometryParam,
                    ["geometryType"] = "esriGeometryEnvelope",
                    ["spatialRel"] = "esriSpatialRelIntersects",
                    ["outFields"] = "*",
                    ["returnGeometry"] = "true",
                    ["f"] = "geojson",
                    ["inSR"] = "4326"  // Uses the WGS 84 coordinate system (longitude/latitude)
                };
                
                // Build query string with proper URL encoding
                var query = string.Join("&", queryParams.Select(kvp => 
                    $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value)}"));
                
                var requestUrl = $"{_arcGisFeatureUrl}?{query}";
                
                _logger.LogDebug("Making ArcGIS request: {Url}, requestId={RequestId}", requestUrl, requestId);
                
                var httpStopwatch = System.Diagnostics.Stopwatch.StartNew();
                var response = await _httpClient.GetAsync(requestUrl);
                httpStopwatch.Stop();
                
                _logger.LogDebug("ArcGIS HTTP response received: status={StatusCode}, elapsed={ElapsedMs}ms, requestId={RequestId}",
                    response.StatusCode, httpStopwatch.ElapsedMilliseconds, requestId);
                
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("ArcGIS API returned error {StatusCode}: {Error}, requestId={RequestId}", 
                        response.StatusCode, errorContent, requestId);
                    response.EnsureSuccessStatusCode(); // This will throw the exception
                }
                
                var geoJson = await response.Content.ReadAsStringAsync();
                
                stopwatch.Stop();
                _logger.LogInformation("Successfully fetched fire perimeter GeoJSON: length={Length} chars, elapsed={ElapsedMs}ms, requestId={RequestId}", 
                    geoJson.Length, stopwatch.ElapsedMilliseconds, requestId);
                
                // Log a sample of the response for debugging
                if (geoJson.Length > 100)
                {
                    _logger.LogDebug("GeoJSON sample (first 200 chars): {Sample}..., requestId={RequestId}", 
                        geoJson[..Math.Min(200, geoJson.Length)], requestId);
                }
                
                return geoJson;
            }
            catch (HttpRequestException httpEx)
            {
                stopwatch.Stop();
                _logger.LogError(httpEx, "HTTP error fetching fire perimeters from ArcGIS after {ElapsedMs}ms, requestId={RequestId}", 
                    stopwatch.ElapsedMilliseconds, requestId);
                return """{"type":"FeatureCollection","features":[]}""";
            }
            catch (TaskCanceledException timeoutEx)
            {
                stopwatch.Stop();
                _logger.LogError(timeoutEx, "Timeout fetching fire perimeters from ArcGIS after {ElapsedMs}ms, requestId={RequestId}", 
                    stopwatch.ElapsedMilliseconds, requestId);
                return """{"type":"FeatureCollection","features":[]}""";
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "Unexpected error fetching fire perimeters from ArcGIS after {ElapsedMs}ms, requestId={RequestId}", 
                    stopwatch.ElapsedMilliseconds, requestId);
                return """{"type":"FeatureCollection","features":[]}""";
            }
        }

        public async Task<List<AvoidRectangle>> TryFetchClosureRectanglesAsync(BoundingBox bbox)
        {
            var requestId = Guid.NewGuid().ToString("N")[..8];
            _logger.LogDebug("Starting closure rectangles fetch: bbox=[{MinLat},{MinLon},{MaxLat},{MaxLon}], requestId={RequestId}",
                bbox.MinLat, bbox.MinLon, bbox.MaxLat, bbox.MaxLon, requestId);
                
            // Stub implementation - in real scenario, this would fetch closure data from another service
            _logger.LogInformation("Fetching closure rectangles - stubbed implementation, requestId={RequestId}", requestId);
            
            // Simulate some processing time
            await Task.Delay(10);
            
            _logger.LogDebug("Closure rectangles fetch completed (stubbed): returning 0 rectangles, requestId={RequestId}", requestId);
            
            // Return empty list for now
            return new List<AvoidRectangle>();
        }
    }
}