using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;
using Microsoft.Extensions.Logging;
using EmergencyManagementMCP.Services;
using EmergencyManagementMCP.Common;
using EmergencyManagementMCP.Models;

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
            [McpToolTrigger(ToolName, ToolDescription)] RoutingFireAwareShortestRequest request,
            ToolInvocationContext context)
        {
            var traceId = Guid.NewGuid().ToString("N")[..8];
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            _logger.LogInformation("Starting fire-aware routing request: origin=({OriginLat},{OriginLon}), destination=({DestLat},{DestLon}), bufferKm={BufferKm}, useClosures={UseClosures}, traceId={TraceId}",
                request.origin.lat, request.origin.lon, request.destination.lat, request.destination.lon, request.bufferKm, request.useClosures, traceId);

            // Validate input parameters
            if (!IsValidCoordinate(request.origin.lat, request.origin.lon))
            {
                _logger.LogError("Invalid origin coordinates: lat={Lat}, lon={Lon}, traceId={TraceId}", 
                    request.origin.lat, request.origin.lon, traceId);
                return CreateErrorResponse("Invalid origin coordinates", traceId);
            }

            if (!IsValidCoordinate(request.destination.lat, request.destination.lon))
            {
                _logger.LogError("Invalid destination coordinates: lat={Lat}, lon={Lon}, traceId={TraceId}", 
                    request.destination.lat, request.destination.lon, traceId);
                return CreateErrorResponse("Invalid destination coordinates", traceId);
            }

            if (request.bufferKm < 0 || request.bufferKm > 100)
            {
                _logger.LogError("Invalid buffer distance: {BufferKm}km, traceId={TraceId}", request.bufferKm, traceId);
                return CreateErrorResponse("Buffer distance must be between 0 and 100 km", traceId);
            }

            try
            {
                var origin = new Coordinate { Lat = request.origin.lat, Lon = request.origin.lon };
                var destination = new Coordinate { Lat = request.destination.lat, Lon = request.destination.lon };

                _logger.LogDebug("Step 1: Computing bounding box, traceId={TraceId}", traceId);
                var stepStopwatch = System.Diagnostics.Stopwatch.StartNew();
                
                // 1. Compute bounding box with buffer
                var bbox = _geometryUtils.ComputeBBox(origin, destination, request.bufferKm);
                
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
                var avoidRectangles = _geometryUtils.BuildAvoidRectanglesFromGeoJson(fireGeoJson, request.bufferKm, 10);
                
                stepStopwatch.Stop();
                _logger.LogDebug("Step 4 completed in {ElapsedMs}ms: {Count} avoid rectangles built, traceId={TraceId}", 
                    stepStopwatch.ElapsedMilliseconds, avoidRectangles.Count, traceId);

                // 5. Optionally add closure rectangles
                if (request.useClosures)
                {
                    _logger.LogDebug("Step 5: Fetching closure rectangles, traceId={TraceId}", traceId);
                    stepStopwatch.Restart();
                    
                    var closureRectangles = await _geoServiceClient.TryFetchClosureRectanglesAsync(bbox);
                    var availableSlots = Math.Max(0, 10 - avoidRectangles.Count);
                    var closuresToAdd = closureRectangles.Take(availableSlots).ToList();
                    avoidRectangles.AddRange(closuresToAdd);
                    
                    stepStopwatch.Stop();
                    _logger.LogDebug("Step 5 completed in {ElapsedMs}ms: {ClosureCount} closures added (of {TotalClosures} available), traceId={TraceId}", 
                        stepStopwatch.ElapsedMilliseconds, closuresToAdd.Count, closureRectangles.Count, traceId);
                }
                else
                {
                    _logger.LogDebug("Step 5: Skipped closure rectangles (useClosures=false), traceId={TraceId}", traceId);
                }

                // 6. Parse departure time if provided
                DateTime? departAt = null;
                if (!string.IsNullOrEmpty(request.departAtIsoUtc))
                {
                    _logger.LogDebug("Step 6: Parsing departure time: {DepartAt}, traceId={TraceId}", request.departAtIsoUtc, traceId);
                    if (DateTime.TryParse(request.departAtIsoUtc, out var parsedDate))
                    {
                        departAt = parsedDate.ToUniversalTime();
                        _logger.LogDebug("Departure time parsed successfully: {DepartAt} UTC, traceId={TraceId}", departAt, traceId);
                    }
                    else
                    {
                        _logger.LogWarning("Failed to parse departure time: {DepartAt}, using current time, traceId={TraceId}", 
                            request.departAtIsoUtc, traceId);
                    }
                }

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
                    PolylineGeoJson = """{"type":"LineString","coordinates":[]}""" 
                },
                AppliedAvoids = new[] { $"ERROR: {errorMessage}" },
                TraceId = traceId
            };
        }

        public const string ToolName = "routing.fireAwareShortest";
        public const string ToolDescription = "Compute the shortest route while avoiding wildfire perimeters and closures.";

        public static readonly IReadOnlyList<McpToolProperty> Properties = new List<McpToolProperty>
        {
            new McpToolProperty { Name = "origin", Type = "object", Description = "Origin coordinate with lat and lon properties." },
            new McpToolProperty { Name = "destination", Type = "object", Description = "Destination coordinate with lat and lon properties." },
            new McpToolProperty { Name = "bufferKm", Type = "number", Description = "Buffer distance in kilometers around fire perimeters to avoid. Default is 2.0." },
            new McpToolProperty { Name = "useClosures", Type = "boolean", Description = "Whether to include road closures in avoidance. Default is true." },
            new McpToolProperty { Name = "departAtIsoUtc", Type = "string", Description = "Optional departure time in ISO 8601 UTC format (e.g., '2025-09-05T13:00:00Z')." }
        };
    }

    // POCO for Azure Function argument binding
    public class RoutingFireAwareShortestRequest
    {
        public OriginDestinationCoordinate origin { get; set; } = new();
        public OriginDestinationCoordinate destination { get; set; } = new();
        public double bufferKm { get; set; } = 2.0;
        public bool useClosures { get; set; } = true;
        public string? departAtIsoUtc { get; set; }
    }

    public class OriginDestinationCoordinate
    {
        public double lat { get; set; }
        public double lon { get; set; }
    }
}