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

        public FireZoneInfo CheckPointInFireZones(string geoJson, Coordinate point)
        {
            var requestId = Guid.NewGuid().ToString("N")[..8];
            _logger.LogDebug("Checking if point ({Lat},{Lon}) is in fire zones, geoJsonSize={Size} chars, requestId={RequestId}",
                point.Lat, point.Lon, geoJson.Length, requestId);

            var fireZoneInfo = new FireZoneInfo { IsInFireZone = false };

            try
            {
                using var doc = JsonDocument.Parse(geoJson);
                var root = doc.RootElement;

                if (!root.TryGetProperty("features", out var features) || features.ValueKind != JsonValueKind.Array)
                {
                    _logger.LogWarning("Invalid GeoJSON: missing or invalid features array, requestId={RequestId}", requestId);
                    return fireZoneInfo;
                }

                var featureCount = features.GetArrayLength();
                _logger.LogDebug("Checking point against {FeatureCount} fire zone features, requestId={RequestId}", 
                    featureCount, requestId);

                foreach (var feature in features.EnumerateArray())
                {
                    if (feature.TryGetProperty("geometry", out var geometry) &&
                        feature.TryGetProperty("properties", out var properties))
                    {
                        if (IsPointInGeometry(point, geometry))
                        {
                            _logger.LogInformation("Point ({Lat},{Lon}) is inside fire zone, requestId={RequestId}", 
                                point.Lat, point.Lon, requestId);

                            fireZoneInfo.IsInFireZone = true;

                            // Extract fire zone details from properties
                            if (properties.TryGetProperty("IncidentName", out var incidentName))
                                fireZoneInfo.IncidentName = incidentName.GetString() ?? "";

                            if (properties.TryGetProperty("FireDiscoveryDateTime", out var discovery))
                                fireZoneInfo.FireZoneName = incidentName.GetString() ?? "";

                            if (properties.TryGetProperty("PercentContained", out var containment))
                                fireZoneInfo.ContainmentPercent = containment.GetDouble();

                            if (properties.TryGetProperty("DailyAcres", out var acres))
                                fireZoneInfo.AcresBurned = acres.GetDouble();

                            if (properties.TryGetProperty("ModifiedOnDateTime", out var modified))
                            {
                                if (DateTime.TryParse(modified.GetString(), out var modifiedDate))
                                    fireZoneInfo.LastUpdate = modifiedDate;
                            }

                            return fireZoneInfo; // Return first match
                        }
                    }
                }

                _logger.LogDebug("Point ({Lat},{Lon}) is not in any fire zone, requestId={RequestId}", 
                    point.Lat, point.Lon, requestId);
            }
            catch (JsonException jsonEx)
            {
                _logger.LogError(jsonEx, "Failed to parse GeoJSON for fire zone check: {Message}, requestId={RequestId}", 
                    jsonEx.Message, requestId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error checking point in fire zones, requestId={RequestId}", requestId);
            }

            return fireZoneInfo;
        }

        private bool IsPointInGeometry(Coordinate point, JsonElement geometry)
        {
            if (!geometry.TryGetProperty("type", out var geometryType) ||
                !geometry.TryGetProperty("coordinates", out var coordinates))
                return false;

            var type = geometryType.GetString();
            
            return type switch
            {
                "Polygon" => IsPointInPolygon(point, coordinates),
                "MultiPolygon" => IsPointInMultiPolygon(point, coordinates),
                _ => false
            };
        }

        private bool IsPointInPolygon(Coordinate point, JsonElement coordinates)
        {
            if (coordinates.ValueKind != JsonValueKind.Array || coordinates.GetArrayLength() == 0)
                return false;

            // Use the first ring (exterior ring) for polygon check
            var exteriorRing = coordinates[0];
            return IsPointInLinearRing(point, exteriorRing);
        }

        private bool IsPointInMultiPolygon(Coordinate point, JsonElement coordinates)
        {
            if (coordinates.ValueKind != JsonValueKind.Array)
                return false;

            foreach (var polygon in coordinates.EnumerateArray())
            {
                if (IsPointInPolygon(point, polygon))
                    return true;
            }

            return false;
        }

        private bool IsPointInLinearRing(Coordinate point, JsonElement ring)
        {
            if (ring.ValueKind != JsonValueKind.Array)
                return false;

            var vertices = new List<(double lat, double lon)>();
            foreach (var coord in ring.EnumerateArray())
            {
                if (coord.ValueKind == JsonValueKind.Array && coord.GetArrayLength() >= 2)
                {
                    var lon = coord[0].GetDouble();
                    var lat = coord[1].GetDouble();
                    vertices.Add((lat, lon));
                }
            }

            return IsPointInPolygonRaycast(point.Lat, point.Lon, vertices);
        }

        // Ray casting algorithm for point-in-polygon test
        private bool IsPointInPolygonRaycast(double testLat, double testLon, List<(double lat, double lon)> vertices)
        {
            if (vertices.Count < 3) return false;

            bool inside = false;
            int j = vertices.Count - 1;

            for (int i = 0; i < vertices.Count; i++)
            {
                var (iLat, iLon) = vertices[i];
                var (jLat, jLon) = vertices[j];

                if (((iLat > testLat) != (jLat > testLat)) &&
                    (testLon < (jLon - iLon) * (testLat - iLat) / (jLat - iLat) + iLon))
                {
                    inside = !inside;
                }
                j = i;
            }

            return inside;
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