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
        }

        public BoundingBox ComputeBBox(Coordinate origin, Coordinate destination, double bufferKm)
        {
            // Find the overall bounding box of the two points
            var minLat = Math.Min(origin.Lat, destination.Lat);
            var maxLat = Math.Max(origin.Lat, destination.Lat);
            var minLon = Math.Min(origin.Lon, destination.Lon);
            var maxLon = Math.Max(origin.Lon, destination.Lon);

            // Apply buffer in degrees (rough approximation)
            var bufferDegrees = bufferKm / 111.0; // Roughly 111 km per degree

            var bbox = new BoundingBox
            {
                MinLat = minLat - bufferDegrees,
                MaxLat = maxLat + bufferDegrees,
                MinLon = minLon - bufferDegrees,
                MaxLon = maxLon + bufferDegrees
            };

            _logger.LogInformation("Computed bbox with {BufferKm}km buffer: {MinLat},{MinLon} to {MaxLat},{MaxLon}", 
                bufferKm, bbox.MinLat, bbox.MinLon, bbox.MaxLat, bbox.MaxLon);

            return bbox;
        }

        public List<AvoidRectangle> BuildAvoidRectanglesFromGeoJson(string geoJson, double bufferKm, int maxRects = 10)
        {
            var rectangles = new List<AvoidRectangle>();

            try
            {
                using var doc = JsonDocument.Parse(geoJson);
                var root = doc.RootElement;

                if (!root.TryGetProperty("features", out var features) || features.ValueKind != JsonValueKind.Array)
                {
                    _logger.LogWarning("Invalid GeoJSON: missing or invalid features array");
                    return rectangles;
                }

                var bufferDegrees = bufferKm / 111.0; // Rough approximation

                foreach (var feature in features.EnumerateArray())
                {
                    if (rectangles.Count >= maxRects)
                    {
                        _logger.LogInformation("Reached maximum rectangle limit: {MaxRects}", maxRects);
                        break;
                    }

                    if (feature.TryGetProperty("geometry", out var geometry) &&
                        geometry.TryGetProperty("type", out var geometryType))
                    {
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
                            _logger.LogDebug("Added avoid rectangle: {Rectangle}", rect.ToString());
                        }
                    }
                }

                _logger.LogInformation("Built {Count} avoid rectangles from GeoJSON with {BufferKm}km buffer", 
                    rectangles.Count, bufferKm);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing GeoJSON for avoid rectangles");
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