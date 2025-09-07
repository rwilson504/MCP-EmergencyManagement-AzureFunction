using EmergencyManagementMCP.Models;
using EmergencyManagementMCP.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;
using Microsoft.Extensions.Logging;

namespace EmergencyManagementMCP.Tools
{
    public class AddressFireZoneCheckTool
    {
        private readonly IGeocodingClient _geocodingClient;
        private readonly IGeoServiceClient _geoServiceClient;
        private readonly IGeoJsonCache _geoJsonCache;
        private readonly IGeometryUtils _geometryUtils;
        private readonly ILogger<AddressFireZoneCheckTool> _logger;

        public AddressFireZoneCheckTool(
            IGeocodingClient geocodingClient,
            IGeoServiceClient geoServiceClient,
            IGeoJsonCache geoJsonCache,
            IGeometryUtils geometryUtils,
            ILogger<AddressFireZoneCheckTool> logger)
        {
            _geocodingClient = geocodingClient;
            _geoServiceClient = geoServiceClient;
            _geoJsonCache = geoJsonCache;
            _geometryUtils = geometryUtils;
            _logger = logger;
        }

        [Function(nameof(AddressFireZoneCheckTool))]
        public async Task<object> Run(
            [McpToolTrigger(ToolName, ToolDescription)] ToolInvocationContext context,
            [McpToolProperty(AddressFireZoneCheckToolPropertyStrings.AddressName, AddressFireZoneCheckToolPropertyStrings.AddressType, AddressFireZoneCheckToolPropertyStrings.AddressDescription, Required = true)] string address
        )
        {
            var traceId = Guid.NewGuid().ToString("N")[..8];
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            _logger.LogInformation("Starting address fire zone check: address=\"{Address}\", traceId={TraceId}",
                address, traceId);

            // Validate input parameters
            if (string.IsNullOrWhiteSpace(address))
            {
                _logger.LogError("Address cannot be null or empty, traceId={TraceId}", traceId);
                return CreateErrorResponse("Address cannot be null or empty", traceId);
            }

            try
            {
                // Step 1: Geocode the address
                _logger.LogDebug("Step 1: Geocoding address, traceId={TraceId}", traceId);
                var stepStopwatch = System.Diagnostics.Stopwatch.StartNew();
                
                var geocodingResult = await _geocodingClient.GeocodeAddressAsync(address);
                
                stepStopwatch.Stop();
                _logger.LogDebug("Step 1 completed in {ElapsedMs}ms: geocoded to ({Lat},{Lon}), traceId={TraceId}", 
                    stepStopwatch.ElapsedMilliseconds, geocodingResult.Coordinates.Lat, geocodingResult.Coordinates.Lon, traceId);

                // Step 2: Create a small bounding box around the point for fire data retrieval
                _logger.LogDebug("Step 2: Computing bounding box around point, traceId={TraceId}", traceId);
                stepStopwatch.Restart();
                
                var pointCoord = geocodingResult.Coordinates;
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
                var response = new AddressFireZoneResponse
                {
                    Geocoding = geocodingResult,
                    FireZone = fireZoneInfo,
                    TraceId = traceId
                };

                stopwatch.Stop();
                _logger.LogInformation("Address fire zone check completed: address=\"{Address}\", coordinates=({Lat},{Lon}), inFireZone={InFireZone}, incidentName=\"{IncidentName}\", totalTime={TotalMs}ms, traceId={TraceId}",
                    address, pointCoord.Lat, pointCoord.Lon, fireZoneInfo.IsInFireZone, fireZoneInfo.IncidentName, stopwatch.ElapsedMilliseconds, traceId);

                return response;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "Error checking address fire zone after {ElapsedMs}ms, traceId={TraceId}", 
                    stopwatch.ElapsedMilliseconds, traceId);
                
                // Return error response with more context
                return CreateErrorResponse($"Fire zone check failed: {ex.Message}", traceId);
            }
        }

        private static object CreateErrorResponse(string errorMessage, string traceId)
        {
            return new AddressFireZoneResponse
            {
                Geocoding = new GeocodingResult
                {
                    Address = "ERROR",
                    Coordinates = new Coordinate { Lat = 0, Lon = 0 },
                    FormattedAddress = $"ERROR: {errorMessage}",
                    Confidence = "None"
                },
                FireZone = new FireZoneInfo
                {
                    IsInFireZone = false,
                    FireZoneName = $"ERROR: {errorMessage}"
                },
                TraceId = traceId
            };
        }

        public const string ToolName = "emergency.addressFireZoneCheck";
        public const string ToolDescription = "Check if a street address is located within an active fire zone and get coordinates.";
    }
}