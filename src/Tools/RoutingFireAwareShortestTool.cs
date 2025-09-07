using EmergencyManagementMCP.Models;
using EmergencyManagementMCP.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;
using Microsoft.Extensions.Logging;

namespace EmergencyManagementMCP.Tools
{
    public class RoutingFireAwareShortestTool
    {
        private readonly IGeoServiceClient _geoServiceClient;
        private readonly IGeoJsonCache _geoJsonCache;
        private readonly IGeometryUtils _geometryUtils;
        private readonly IRouterClient _routerClient;
        private readonly ILogger<RoutingFireAwareShortestTool> _logger;

        public RoutingFireAwareShortestTool(
            IGeoServiceClient geoServiceClient,
            IGeoJsonCache geoJsonCache,
            IGeometryUtils geometryUtils,
            IRouterClient routerClient,
            ILogger<RoutingFireAwareShortestTool> logger)
        {
            _geoServiceClient = geoServiceClient;
            _geoJsonCache = geoJsonCache;
            _geometryUtils = geometryUtils;
            _routerClient = routerClient;
            _logger = logger;
        }

        [Function(nameof(RoutingFireAwareShortestTool))]
        public async Task<object> Run(
            [McpToolTrigger(ToolName, ToolDescription)] ToolInvocationContext context,
            [McpToolProperty(RoutingFireAwareShortestToolPropertyStrings.OriginLatName, RoutingFireAwareShortestToolPropertyStrings.OriginLatType, RoutingFireAwareShortestToolPropertyStrings.OriginLatDescription, Required = true)] double originLat,
            [McpToolProperty(RoutingFireAwareShortestToolPropertyStrings.OriginLonName, RoutingFireAwareShortestToolPropertyStrings.OriginLonType, RoutingFireAwareShortestToolPropertyStrings.OriginLonDescription, Required = true)] double originLon,
            [McpToolProperty(RoutingFireAwareShortestToolPropertyStrings.DestinationLatName, RoutingFireAwareShortestToolPropertyStrings.DestinationLatType, RoutingFireAwareShortestToolPropertyStrings.DestinationLatDescription, Required = true)] double destinationLat,
            [McpToolProperty(RoutingFireAwareShortestToolPropertyStrings.DestinationLonName, RoutingFireAwareShortestToolPropertyStrings.DestinationLonType, RoutingFireAwareShortestToolPropertyStrings.DestinationLonDescription, Required = true)] double destinationLon,
            [McpToolProperty(RoutingFireAwareShortestToolPropertyStrings.AvoidBufferMetersName, RoutingFireAwareShortestToolPropertyStrings.AvoidBufferMetersType, RoutingFireAwareShortestToolPropertyStrings.AvoidBufferMetersDescription, Required = false)] double? avoidBufferMeters,
            [McpToolProperty(RoutingFireAwareShortestToolPropertyStrings.DepartAtIsoUtcName, RoutingFireAwareShortestToolPropertyStrings.DepartAtIsoUtcType, RoutingFireAwareShortestToolPropertyStrings.DepartAtIsoUtcDescription, Required = false)] string? departAtIsoUtc,
            [McpToolProperty(RoutingFireAwareShortestToolPropertyStrings.ProfileName, RoutingFireAwareShortestToolPropertyStrings.ProfileType, RoutingFireAwareShortestToolPropertyStrings.ProfileDescription, Required = false)] string? profile
        )
        {
            var traceId = Guid.NewGuid().ToString("N")[..8];
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            _logger.LogInformation("Starting fire-aware routing request: origin=({OriginLat},{OriginLon}), destination=({DestinationLat},{DestinationLon}), avoidBufferMeters={AvoidBufferMeters}, profile={Profile}, departAtIsoUtc={DepartAtIsoUtc}, traceId={TraceId}",
                originLat, originLon, destinationLat, destinationLon, avoidBufferMeters, profile, departAtIsoUtc, traceId);

            // Validate input parameters
            if (!IsValidCoordinate(originLat, originLon))
            {
                _logger.LogError("Invalid origin coordinates: lat={Lat}, lon={Lon}, traceId={TraceId}", 
                    originLat, originLon, traceId);
                return CreateErrorResponse("Invalid origin coordinates", traceId);
            }

            if (!IsValidCoordinate(destinationLat, destinationLon))
            {
                _logger.LogError("Invalid destination coordinates: lat={Lat}, lon={Lon}, traceId={TraceId}", 
                    destinationLat, destinationLon, traceId);
                return CreateErrorResponse("Invalid destination coordinates", traceId);
            }

            double bufferKm = (avoidBufferMeters ?? 2000) / 1000.0;
            if (bufferKm < 0 || bufferKm > 100)
            {
                _logger.LogError("Invalid buffer distance: {BufferKm}km, traceId={TraceId}", bufferKm, traceId);
                return CreateErrorResponse("Buffer distance must be between 0 and 100 km", traceId);
            }

            DateTime? departAt = null;
            if (!string.IsNullOrEmpty(departAtIsoUtc))
            {
                if (DateTime.TryParse(departAtIsoUtc, out var parsedDt))
                {
                    departAt = parsedDt.ToUniversalTime();
                }
                else
                {
                    _logger.LogError("Invalid DepartAtIsoUtc format: {DepartAtIsoUtc}, traceId={TraceId}", 
                        departAtIsoUtc, traceId);
                    return CreateErrorResponse("Invalid DepartAtIsoUtc format", traceId);
                }
            }

            try
            {
                var origin = new Coordinate { Lat = originLat, Lon = originLon };
                var destination = new Coordinate { Lat = destinationLat, Lon = destinationLon };

                _logger.LogDebug("Step 1: Computing bounding box, traceId={TraceId}", traceId);
                var stepStopwatch = System.Diagnostics.Stopwatch.StartNew();
                
                // 1. Compute bounding box with buffer
                var bbox = _geometryUtils.ComputeBBox(origin, destination, bufferKm);
                
                stepStopwatch.Stop();
                _logger.LogDebug("Step 1 completed in {ElapsedMs}ms: bbox computed, traceId={TraceId}", 
                    stepStopwatch.ElapsedMilliseconds, traceId);

                // 2. Cache key for fire perimeters
                var cacheKey = $"fire-perimeters-{bbox.MinLat:F3}-{bbox.MinLon:F3}-{bbox.MaxLat:F3}-{bbox.MaxLon:F3}";
                _logger.LogDebug("Step 2: Using cache key: {CacheKey}, traceId={TraceId}", cacheKey, traceId);

                _logger.LogDebug("Step 3: Loading fire perimeter data, traceId={TraceId}", traceId);
                stepStopwatch.Restart();
                
                // 3. Load or refresh fire GeoJSON data (with 10-minute TTL)
                var fireGeoJson = await _geoJsonCache.LoadOrRefreshAsync(
                    cacheKey,
                    TimeSpan.FromMinutes(10),
                    () => _geoServiceClient.FetchPerimetersAsGeoJsonAsync(bbox, 60));

                stepStopwatch.Stop();
                _logger.LogDebug("Step 3 completed in {ElapsedMs}ms: fire data loaded, size={Size} chars, traceId={TraceId}", 
                    stepStopwatch.ElapsedMilliseconds, fireGeoJson.Length, traceId);

                _logger.LogDebug("Step 4: Building avoid rectangles from fire data, traceId={TraceId}", traceId);
                stepStopwatch.Restart();
                
                // 4. Build avoid rectangles from fire perimeters (cap at 10)
                var avoidRectangles = _geometryUtils.BuildAvoidRectanglesFromGeoJson(fireGeoJson, bufferKm, 10);
                
                stepStopwatch.Stop();
                _logger.LogDebug("Step 4 completed in {ElapsedMs}ms: {Count} avoid rectangles built, traceId={TraceId}", 
                    stepStopwatch.ElapsedMilliseconds, avoidRectangles.Count, traceId);

                // 5. Optionally add closure rectangles (always included for flat model)
                _logger.LogDebug("Step 5: Fetching closure rectangles, traceId={TraceId}", traceId);
                stepStopwatch.Restart();
                
                var closureRectangles = await _geoServiceClient.TryFetchClosureRectanglesAsync(bbox);
                var availableSlots = Math.Max(0, 10 - avoidRectangles.Count);
                var closuresToAdd = closureRectangles.Take(availableSlots).ToList();
                avoidRectangles.AddRange(closuresToAdd);
                
                stepStopwatch.Stop();
                _logger.LogDebug("Step 5 completed in {ElapsedMs}ms: {ClosureCount} closures added (of {TotalClosures} available), traceId={TraceId}", 
                    stepStopwatch.ElapsedMilliseconds, closuresToAdd.Count, closureRectangles.Count, traceId);

                // 6. Profile (not used in routing logic here, but could be passed to router)
                string routingProfile = profile ?? "driving";

                _logger.LogDebug("Step 7: Calculating route with {AvoidCount} avoid areas, traceId={TraceId}", 
                    avoidRectangles.Count, traceId);
                stepStopwatch.Restart();
                
                // 7. Call router with avoid areas
                var route = await _routerClient.GetRouteAsync(origin, destination, avoidRectangles, departAt);

                stepStopwatch.Stop();
                _logger.LogDebug("Step 7 completed in {ElapsedMs}ms: route calculated, traceId={TraceId}", 
                    stepStopwatch.ElapsedMilliseconds, traceId);

                // 8. Build response
                var response = new FireAwareRouteResponse
                {
                    Route = route,
                    AppliedAvoids = avoidRectangles.Select(r => r.ToString()).ToArray(),
                    TraceId = traceId
                };

                stopwatch.Stop();
                _logger.LogInformation("Fire-aware route calculated successfully: distance={Distance}m, time={Time}s, avoids={AvoidCount}, totalTime={TotalMs}ms, traceId={TraceId}",
                    route.DistanceMeters, route.TravelTimeSeconds, avoidRectangles.Count, stopwatch.ElapsedMilliseconds, traceId);

                return response;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "Error calculating fire-aware route after {ElapsedMs}ms, traceId={TraceId}", 
                    stopwatch.ElapsedMilliseconds, traceId);
                
                // Return error response with more context
                return CreateErrorResponse($"Route calculation failed: {ex.Message}", traceId);
            }
        }

        private static bool IsValidCoordinate(double lat, double lon)
        {
            return lat >= -90 && lat <= 90 && lon >= -180 && lon <= 180;
        }

        private static object CreateErrorResponse(string errorMessage, string traceId)
        {
            return new FireAwareRouteResponse
            {
                Route = new RouteResult 
                { 
                    DistanceMeters = 0, 
                    TravelTimeSeconds = 0, 
                    PolylineGeoJson = """{"type":"LineString","coordinates":[]}""",
                    DrivingDirections = Array.Empty<DrivingInstruction>()
                },
                AppliedAvoids = new[] { $"ERROR: {errorMessage}" },
                TraceId = traceId
            };
        }

        public const string ToolName = "routing.fireAwareShortest";
        public const string ToolDescription = "Compute the shortest route while avoiding wildfire perimeters and closures (coordinate-based).";
    }
}