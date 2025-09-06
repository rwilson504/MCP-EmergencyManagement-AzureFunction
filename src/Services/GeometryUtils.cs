using EmergencyManagementMCP.Models;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace EmergencyManagementMCP.Services
{
    public class GeometryUtils : IGeometryUtils
    {
        private readonly ILogger<GeometryUtils> _logger;
        private const double EarthRadiusKm = 6371.0;

        public GeometryUtils(ILogger<GeometryUtils> logger)
        {
            _logger = logger;
            _logger.LogDebug("GeometryUtils initialized");
        }

        public BoundingBox ComputeBBox(Coordinate origin, Coordinate destination, double bufferKm)
        {
            var requestId = Guid.NewGuid().ToString("N")[..8];
            _logger.LogDebug("Computing bounding box: origin=[{OriginLat},{OriginLon}], destination=[{DestLat},{DestLon}], buffer={BufferKm}km, requestId={RequestId}",
                origin.Lat, origin.Lon, destination.Lat, destination.Lon, bufferKm, requestId);
                
            // Find the overall bounding box of the two points
            var minLat = Math.Min(origin.Lat, destination.Lat);
            var maxLat = Math.Max(origin.Lat, destination.Lat);
            var minLon = Math.Min(origin.Lon, destination.Lon);
            var maxLon = Math.Max(origin.Lon, destination.Lon);

            // Apply buffer in degrees (rough approximation)
            var bufferDegrees = bufferKm / 111.0; // Roughly 111 km per degree
            
            _logger.LogDebug("Buffer calculation: {BufferKm}km = {BufferDegrees} degrees, requestId={RequestId}", 
                bufferKm, bufferDegrees, requestId);

            var bbox = new BoundingBox
            {
                MinLat = minLat - bufferDegrees,
                MaxLat = maxLat + bufferDegrees,
                MinLon = minLon - bufferDegrees,
                MaxLon = maxLon + bufferDegrees
            };

            _logger.LogInformation("Computed bbox with {BufferKm}km buffer: [{MinLat},{MinLon}] to [{MaxLat},{MaxLon}], requestId={RequestId}", 
                bufferKm, bbox.MinLat, bbox.MinLon, bbox.MaxLat, bbox.MaxLon, requestId);

            return bbox;
        }

        public List<AvoidRectangle> BuildAvoidRectanglesFromGeoJson(string geoJson, double bufferKm, int maxRects = 10)
        {
            var requestId = Guid.NewGuid().ToString("N")[..8];
            var rectangles = new List<AvoidRectangle>();
            
            _logger.LogDebug("Building avoid rectangles: geoJsonSize={Size} chars, buffer={BufferKm}km, maxRects={MaxRects}, requestId={RequestId}",
                geoJson.Length, bufferKm, maxRects, requestId);

            try
            {
                using var doc = JsonDocument.Parse(geoJson);
                var root = doc.RootElement;

                if (!root.TryGetProperty("features", out var features) || features.ValueKind != JsonValueKind.Array)
                {
                    _logger.LogWarning("Invalid GeoJSON: missing or invalid features array, requestId={RequestId}", requestId);
                    return rectangles;
                }

                var bufferDegrees = bufferKm / 111.0; // Rough approximation
                var featureCount = features.GetArrayLength();
                
                _logger.LogDebug("Processing {FeatureCount} GeoJSON features with {BufferDegrees} degree buffer, requestId={RequestId}", 
                    featureCount, bufferDegrees, requestId);

                foreach (var feature in features.EnumerateArray())
                {
                    if (rectangles.Count >= maxRects)
                    {
                        _logger.LogInformation("Reached maximum rectangle limit: {MaxRects}, requestId={RequestId}", maxRects, requestId);
                        break;
                    }

                    if (feature.TryGetProperty("geometry", out var geometry) &&
                        geometry.TryGetProperty("type", out var geometryType))
                    {
                        _logger.LogDebug("Processing feature with geometry type: {GeometryType}, requestId={RequestId}", 
                            geometryType.GetString(), requestId);
                            
                        var bbox = ExtractBoundingBox(geometry);
                        if (bbox != null)
                        {
                            // Apply buffer to the bounding box
                            var rect = new AvoidRectangle
                            {
                                MinLat = bbox.MinLat - bufferDegrees,
                                MaxLat = bbox.MaxLat + bufferDegrees,
                                MinLon = bbox.MinLon - bufferDegrees,
                                MaxLon = bbox.MaxLon + bufferDegrees
                            };
                            
                            rectangles.Add(rect);
                            _logger.LogDebug("Added avoid rectangle #{Index}: {Rectangle}, requestId={RequestId}", 
                                rectangles.Count, rect.ToString(), requestId);
                        }
                        else
                        {
                            _logger.LogDebug("Could not extract bounding box from geometry, requestId={RequestId}", requestId);
                        }
                    }
                    else
                    {
                        _logger.LogDebug("Feature missing geometry or type property, requestId={RequestId}", requestId);
                    }
                }

                _logger.LogInformation("Built {Count} avoid rectangles from GeoJSON with {BufferKm}km buffer (processed {FeatureCount} features), requestId={RequestId}", 
                    rectangles.Count, bufferKm, featureCount, requestId);
            }
            catch (JsonException jsonEx)
            {
                _logger.LogError(jsonEx, "Failed to parse GeoJSON: {Message}, requestId={RequestId}", jsonEx.Message, requestId);
                return rectangles;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error building avoid rectangles from GeoJSON, requestId={RequestId}", requestId);
                return rectangles;
            }

            return rectangles;
        }

        private BoundingBox? ExtractBoundingBox(JsonElement geometry)
        {
            if (!geometry.TryGetProperty("coordinates", out var coordinates))
                return null;

            var allCoords = new List<double[]>();
            ExtractCoordinatesRecursive(coordinates, allCoords);

            if (allCoords.Count == 0)
                return null;

            var lons = allCoords.Select(c => c[0]).ToArray();
            var lats = allCoords.Select(c => c[1]).ToArray();

            return new BoundingBox
            {
                MinLon = lons.Min(),
                MaxLon = lons.Max(),
                MinLat = lats.Min(),
                MaxLat = lats.Max()
            };
        }

        private void ExtractCoordinatesRecursive(JsonElement element, List<double[]> coords)
        {
            if (element.ValueKind == JsonValueKind.Array)
            {
                var firstElement = element.EnumerateArray().FirstOrDefault();
                
                // Check if this is a coordinate pair (array of numbers)
                if (firstElement.ValueKind == JsonValueKind.Number)
                {
                    var coord = element.EnumerateArray().Select(e => e.GetDouble()).ToArray();
                    if (coord.Length >= 2)
                    {
                        coords.Add(coord);
                    }
                }
                else
                {
                    // Recurse into nested arrays
                    foreach (var child in element.EnumerateArray())
                    {
                        ExtractCoordinatesRecursive(child, coords);
                    }
                }
            }
        }
    }
}