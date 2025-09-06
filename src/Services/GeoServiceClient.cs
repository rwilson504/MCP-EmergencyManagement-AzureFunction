using EmergencyManagementMCP.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System.Text.Json;

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
            _arcGisFeatureUrl = config["Fires__ArcGisFeatureUrl"] ?? "https://services3.arcgis.com/T4QMspbfLg3qTGWY/arcgis/rest/services/WFIGS_Wildland_Fire_Perimeters_ToDate/FeatureServer/0/query";
        }

        public async Task<string> FetchPerimetersAsGeoJsonAsync(BoundingBox bbox, int sinceMins = 60)
        {
            try
            {
                var sinceDate = DateTime.UtcNow.AddMinutes(-sinceMins).ToString("yyyy-MM-dd HH:mm:ss");
                
                var query = $"where=1=1&geometry={bbox.MinLon},{bbox.MinLat},{bbox.MaxLon},{bbox.MaxLat}&geometryType=esriGeometryEnvelope&spatialRel=esriSpatialRelIntersects&outFields=*&returnGeometry=true&f=geojson";
                
                var requestUrl = $"{_arcGisFeatureUrl}?{query}";
                
                _logger.LogInformation("Fetching fire perimeters from ArcGIS: {Url}", requestUrl);
                
                var response = await _httpClient.GetAsync(requestUrl);
                response.EnsureSuccessStatusCode();
                
                var geoJson = await response.Content.ReadAsStringAsync();
                
                _logger.LogInformation("Successfully fetched fire perimeter GeoJSON, length: {Length} chars", geoJson.Length);
                
                return geoJson;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch fire perimeters from ArcGIS");
                // Return empty FeatureCollection on error
                return """{"type":"FeatureCollection","features":[]}""";
            }
        }

        public async Task<List<AvoidRectangle>> TryFetchClosureRectanglesAsync(BoundingBox bbox)
        {
            // Stub implementation - in real scenario, this would fetch closure data from another service
            _logger.LogInformation("Fetching closure rectangles - stubbed implementation");
            
            // Return empty list for now
            return new List<AvoidRectangle>();
        }
    }
}