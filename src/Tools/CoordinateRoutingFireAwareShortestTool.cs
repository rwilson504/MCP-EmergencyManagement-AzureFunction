using EmergencyManagementMCP.Models;
using EmergencyManagementMCP.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;
using Microsoft.Extensions.Logging;

namespace EmergencyManagementMCP.Tools
{
    public class CoordinateRoutingFireAwareShortestTool
    {
        private readonly IGeoServiceClient _geoServiceClient;
        private readonly IGeoJsonCache _geoJsonCache;
        private readonly IGeometryUtils _geometryUtils;
        private readonly IRouterClient _routerClient;
    private readonly IRouteLinkService _routeLinkService;
        private readonly ILogger<CoordinateRoutingFireAwareShortestTool> _logger;

        public CoordinateRoutingFireAwareShortestTool(
            IGeoServiceClient geoServiceClient,
            IGeoJsonCache geoJsonCache,
            IGeometryUtils geometryUtils,
            IRouterClient routerClient,
            IRouteLinkService routeLinkService,
            ILogger<CoordinateRoutingFireAwareShortestTool> logger)
        {
            _geoServiceClient = geoServiceClient;
            _geoJsonCache = geoJsonCache;
            _geometryUtils = geometryUtils;
            _routerClient = routerClient;
            _routeLinkService = routeLinkService;
            _logger = logger;
        }

        [Function(nameof(CoordinateRoutingFireAwareShortestTool))]
        public async Task<object> Run(
            [McpToolTrigger(ToolName, ToolDescription)] ToolInvocationContext context,
            [McpToolProperty(CoordinateRoutingFireAwareShortestToolPropertyStrings.OriginLatName, CoordinateRoutingFireAwareShortestToolPropertyStrings.OriginLatType, CoordinateRoutingFireAwareShortestToolPropertyStrings.OriginLatDescription, Required = true)] double originLat,
            [McpToolProperty(CoordinateRoutingFireAwareShortestToolPropertyStrings.OriginLonName, CoordinateRoutingFireAwareShortestToolPropertyStrings.OriginLonType, CoordinateRoutingFireAwareShortestToolPropertyStrings.OriginLonDescription, Required = true)] double originLon,
            [McpToolProperty(CoordinateRoutingFireAwareShortestToolPropertyStrings.DestinationLatName, CoordinateRoutingFireAwareShortestToolPropertyStrings.DestinationLatType, CoordinateRoutingFireAwareShortestToolPropertyStrings.DestinationLatDescription, Required = true)] double destinationLat,
            [McpToolProperty(CoordinateRoutingFireAwareShortestToolPropertyStrings.DestinationLonName, CoordinateRoutingFireAwareShortestToolPropertyStrings.DestinationLonType, CoordinateRoutingFireAwareShortestToolPropertyStrings.DestinationLonDescription, Required = true)] double destinationLon,
            [McpToolProperty(CoordinateRoutingFireAwareShortestToolPropertyStrings.AvoidBufferMetersName, CoordinateRoutingFireAwareShortestToolPropertyStrings.AvoidBufferMetersType, CoordinateRoutingFireAwareShortestToolPropertyStrings.AvoidBufferMetersDescription, Required = false)] double? avoidBufferMeters,
            [McpToolProperty(CoordinateRoutingFireAwareShortestToolPropertyStrings.DepartAtIsoUtcName, CoordinateRoutingFireAwareShortestToolPropertyStrings.DepartAtIsoUtcType, CoordinateRoutingFireAwareShortestToolPropertyStrings.DepartAtIsoUtcDescription, Required = false)] string? departAtIsoUtc,
            [McpToolProperty(CoordinateRoutingFireAwareShortestToolPropertyStrings.ProfileName, CoordinateRoutingFireAwareShortestToolPropertyStrings.ProfileType, CoordinateRoutingFireAwareShortestToolPropertyStrings.ProfileDescription, Required = false)] string? profile,
            [McpToolProperty(CoordinateRoutingFireAwareShortestToolPropertyStrings.PersistShareLinkName, CoordinateRoutingFireAwareShortestToolPropertyStrings.PersistShareLinkType, CoordinateRoutingFireAwareShortestToolPropertyStrings.PersistShareLinkDescription, Required = false)] bool? persistShareLink,
            [McpToolProperty(CoordinateRoutingFireAwareShortestToolPropertyStrings.ShareLinkTtlMinutesName, CoordinateRoutingFireAwareShortestToolPropertyStrings.ShareLinkTtlMinutesType, CoordinateRoutingFireAwareShortestToolPropertyStrings.ShareLinkTtlMinutesDescription, Required = false)] int? shareLinkTtlMinutes
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
                // Apply default: if persistShareLink not provided, default to true to create a share link by default.
                bool persistShareLinkEffective = persistShareLink ?? true;
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
                var routeWithData = await _routerClient.GetRouteWithRequestDataAsync(origin, destination, avoidRectangles, departAt);
                var route = routeWithData.Route;

                stepStopwatch.Stop();
                _logger.LogDebug("Step 7 completed in {ElapsedMs}ms: route calculated, traceId={TraceId}", 
                    stepStopwatch.ElapsedMilliseconds, traceId);

                // 8. Build response
                var appliedAvoidsArr = avoidRectangles.Select(r => r.ToString()).ToArray();
                RouteLink? shareLink = null;
                if (persistShareLinkEffective)
                {
                    try
                    {
                        var ttl = (shareLinkTtlMinutes.HasValue && shareLinkTtlMinutes.Value > 0)
                            ? TimeSpan.FromMinutes(shareLinkTtlMinutes.Value)
                            : (TimeSpan?)null;
                        shareLink = await _routeLinkService.CreateAsync(origin, destination, appliedAvoidsArr, routeWithData.AzureMapsPostJson, ttl);
                    }
                    catch (Exception linkEx)
                    {
                        _logger.LogWarning(linkEx, "Failed to create share link, continuing without it. traceId={TraceId}", traceId);
                    }
                }

                var response = new FireAwareRouteResponse
                {
                    Route = route,
                    AppliedAvoids = appliedAvoidsArr,
                    TraceId = traceId,
                    ShareLink = shareLink,
                    Envelope = new ResponseEnvelope
                    {
                        GeneratedAtUtc = DateTime.UtcNow,
                        LatencyMs = stopwatch.ElapsedMilliseconds,
                        Status = "ok",
                        ToolVersion = "1.1.0"
                    }
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
                return CreateErrorResponse($"Route calculation failed: {ex.Message}", traceId, stopwatch.ElapsedMilliseconds);
            }
        }

        private static bool IsValidCoordinate(double lat, double lon)
        {
            return lat >= -90 && lat <= 90 && lon >= -180 && lon <= 180;
        }

        private static object CreateErrorResponse(string errorMessage, string traceId, long? latencyMs = null)
        {
            return new FireAwareRouteResponse
            {
                Route = new RouteResult 
                { 
                    DistanceMeters = 0, 
                    TravelTimeSeconds = 0, 
                    //PolylineGeoJson = """{"type":"LineString","coordinates":[]}""",
                    DrivingDirections = Array.Empty<DrivingInstruction>()
                },
                AppliedAvoids = new[] { $"ERROR: {errorMessage}" },
                TraceId = traceId,
                ShareLink = null,
                Envelope = new ResponseEnvelope
                {
                    GeneratedAtUtc = DateTime.UtcNow,
                    LatencyMs = latencyMs ?? 0,
                    Status = "error",
                    ToolVersion = "1.1.0"
                }
            };
        }

        public const string ToolName = "routing.coordinateFireAwareShortest";
        public const string ToolDescription = "Compute the shortest route while avoiding wildfire perimeters and closures (coordinate-based).";
    }
}