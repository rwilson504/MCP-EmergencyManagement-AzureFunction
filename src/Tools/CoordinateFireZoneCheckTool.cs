using EmergencyManagementMCP.Models;
using EmergencyManagementMCP.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;
using Microsoft.Extensions.Logging;

namespace EmergencyManagementMCP.Tools
{
    public class CoordinateFireZoneCheckTool
    {
        private readonly IGeoServiceClient _geoServiceClient;
        private readonly IGeoJsonCache _geoJsonCache;
        private readonly IGeometryUtils _geometryUtils;
        private readonly ILogger<CoordinateFireZoneCheckTool> _logger;

        public CoordinateFireZoneCheckTool(
            IGeoServiceClient geoServiceClient,
            IGeoJsonCache geoJsonCache,
            IGeometryUtils geometryUtils,
            ILogger<CoordinateFireZoneCheckTool> logger)
        {
            _geoServiceClient = geoServiceClient;
            _geoJsonCache = geoJsonCache;
            _geometryUtils = geometryUtils;
            _logger = logger;
        }

        [Function(nameof(CoordinateFireZoneCheckTool))]
        public async Task<object> Run(
            [McpToolTrigger(ToolName, ToolDescription)] ToolInvocationContext context,
            [McpToolProperty(CoordinateFireZoneCheckToolPropertyStrings.LatName, CoordinateFireZoneCheckToolPropertyStrings.LatType, CoordinateFireZoneCheckToolPropertyStrings.LatDescription, Required = true)] double lat,
            [McpToolProperty(CoordinateFireZoneCheckToolPropertyStrings.LonName, CoordinateFireZoneCheckToolPropertyStrings.LonType, CoordinateFireZoneCheckToolPropertyStrings.LonDescription, Required = true)] double lon
        )
        {
            var traceId = Guid.NewGuid().ToString("N")[..8];
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            _logger.LogInformation("Starting coordinate fire zone check: coordinates=({Lat},{Lon}), traceId={TraceId}",
                lat, lon, traceId);

            // Validate input parameters
            if (!IsValidCoordinate(lat, lon))
            {
                _logger.LogError("Invalid coordinates: lat={Lat}, lon={Lon}, traceId={TraceId}", lat, lon, traceId);
                return CreateErrorResponse("Invalid coordinates", lat, lon, traceId);
            }

            try
            {
                // Step 1: Create coordinate object
                _logger.LogDebug("Step 1: Creating coordinate object, traceId={TraceId}", traceId);
                var stepStopwatch = System.Diagnostics.Stopwatch.StartNew();
                
                var pointCoord = new Coordinate { Lat = lat, Lon = lon };
                
                stepStopwatch.Stop();
                _logger.LogDebug("Step 1 completed in {ElapsedMs}ms: coordinate object created, traceId={TraceId}", 
                    stepStopwatch.ElapsedMilliseconds, traceId);

                // Step 2: Create a small bounding box around the point for fire data retrieval
                _logger.LogDebug("Step 2: Computing bounding box around point, traceId={TraceId}", traceId);
                stepStopwatch.Restart();
                
                var bbox = _geometryUtils.ComputeBBox(pointCoord, pointCoord, 5.0); // 5km buffer for data retrieval
                
                stepStopwatch.Stop();
                _logger.LogDebug("Step 2 completed in {ElapsedMs}ms: bbox computed, traceId={TraceId}", 
                    stepStopwatch.ElapsedMilliseconds, traceId);

                // Step 3: Cache key for fire perimeters around the point
                var cacheKey = $"fire-perimeters-point-{pointCoord.Lat:F3}-{pointCoord.Lon:F3}";
                _logger.LogDebug("Step 3: Using cache key: {CacheKey}, traceId={TraceId}", cacheKey, traceId);

                // Step 4: Load or refresh fire GeoJSON data (with 10-minute TTL)
                _logger.LogDebug("Step 4: Loading fire perimeter data, traceId={TraceId}", traceId);
                stepStopwatch.Restart();
                
                var fireGeoJson = await _geoJsonCache.LoadOrRefreshAsync(
                    cacheKey,
                    TimeSpan.FromMinutes(10),
                    () => _geoServiceClient.FetchPerimetersAsGeoJsonAsync(bbox, 60));

                stepStopwatch.Stop();
                _logger.LogDebug("Step 4 completed in {ElapsedMs}ms: fire data loaded, size={Size} chars, traceId={TraceId}", 
                    stepStopwatch.ElapsedMilliseconds, fireGeoJson.Length, traceId);

                // Step 5: Check if the point is within any fire zones
                _logger.LogDebug("Step 5: Checking point against fire zones, traceId={TraceId}", traceId);
                stepStopwatch.Restart();
                
                var fireZoneInfo = _geometryUtils.CheckPointInFireZones(fireGeoJson, pointCoord);
                
                stepStopwatch.Stop();
                _logger.LogDebug("Step 5 completed in {ElapsedMs}ms: fire zone check result={IsInFireZone}, traceId={TraceId}", 
                    stepStopwatch.ElapsedMilliseconds, fireZoneInfo.IsInFireZone, traceId);

                // Step 6: Build response
                var response = new CoordinateFireZoneResponse
                {
                    Coordinates = pointCoord,
                    FireZone = fireZoneInfo,
                    TraceId = traceId
                };

                stopwatch.Stop();
                _logger.LogInformation("Coordinate fire zone check completed: coordinates=({Lat},{Lon}), inFireZone={InFireZone}, incidentName=\"{IncidentName}\", totalTime={TotalMs}ms, traceId={TraceId}",
                    lat, lon, fireZoneInfo.IsInFireZone, fireZoneInfo.IncidentName, stopwatch.ElapsedMilliseconds, traceId);

                return response;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "Error checking coordinate fire zone after {ElapsedMs}ms, traceId={TraceId}", 
                    stopwatch.ElapsedMilliseconds, traceId);
                
                // Return error response with more context
                return CreateErrorResponse($"Fire zone check failed: {ex.Message}", lat, lon, traceId);
            }
        }

        private static bool IsValidCoordinate(double lat, double lon)
        {
            return lat >= -90 && lat <= 90 && lon >= -180 && lon <= 180;
        }

        private static object CreateErrorResponse(string errorMessage, double lat, double lon, string traceId)
        {
            return new CoordinateFireZoneResponse
            {
                Coordinates = new Coordinate { Lat = lat, Lon = lon },
                FireZone = new FireZoneInfo
                {
                    IsInFireZone = false,
                    FireZoneName = $"ERROR: {errorMessage}"
                },
                TraceId = traceId
            };
        }

        public const string ToolName = "emergency.coordinateFireZoneCheck";
        public const string ToolDescription = "Check if latitude/longitude coordinates are located within an active fire zone.";
    }
}