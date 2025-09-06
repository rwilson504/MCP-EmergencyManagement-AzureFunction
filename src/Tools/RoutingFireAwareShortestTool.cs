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

            _logger.LogInformation("Starting fire-aware routing request: origin=({OriginLat},{OriginLon}), destination=({DestinationLat},{DestinationLon}), avoidBufferMeters={AvoidBufferMeters}, profile={Profile}, departAtIsoUtc={DepartAtIsoUtc}, traceId={TraceId}",
                request.OriginLat, request.OriginLon, request.DestinationLat, request.DestinationLon, request.AvoidBufferMeters, request.Profile, request.DepartAtIsoUtc, traceId);

            // Validate input parameters
            if (!IsValidCoordinate(request.OriginLat, request.OriginLon))
            {
                _logger.LogError("Invalid origin coordinates: lat={Lat}, lon={Lon}, traceId={TraceId}", 
                    request.OriginLat, request.OriginLon, traceId);
                return CreateErrorResponse("Invalid origin coordinates", traceId);
            }

            if (!IsValidCoordinate(request.DestinationLat, request.DestinationLon))
            {
                _logger.LogError("Invalid destination coordinates: lat={Lat}, lon={Lon}, traceId={TraceId}", 
                    request.DestinationLat, request.DestinationLon, traceId);
                return CreateErrorResponse("Invalid destination coordinates", traceId);
            }

            double bufferKm = (request.AvoidBufferMeters ?? 2000) / 1000.0;
            if (bufferKm < 0 || bufferKm > 100)
            {
                _logger.LogError("Invalid buffer distance: {BufferKm}km, traceId={TraceId}", bufferKm, traceId);
                return CreateErrorResponse("Buffer distance must be between 0 and 100 km", traceId);
            }

            DateTime? departAt = null;
            if (!string.IsNullOrEmpty(request.DepartAtIsoUtc))
            {
                if (DateTime.TryParse(request.DepartAtIsoUtc, out var parsedDt))
                {
                    departAt = parsedDt.ToUniversalTime();
                }
                else
                {
                    _logger.LogError("Invalid DepartAtIsoUtc format: {DepartAtIsoUtc}, traceId={TraceId}", 
                        request.DepartAtIsoUtc, traceId);
                    return CreateErrorResponse("Invalid DepartAtIsoUtc format", traceId);
                }
            }

            try
            {
                var origin = new Coordinate { Lat = request.OriginLat, Lon = request.OriginLon };
                var destination = new Coordinate { Lat = request.DestinationLat, Lon = request.DestinationLon };

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
                string profile = request.Profile ?? "driving";

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
            new McpToolProperty { Name = "OriginLat", Type = "number", Description = "Origin latitude.", Required = true },
            new McpToolProperty { Name = "OriginLon", Type = "number", Description = "Origin longitude.", Required = true },
            new McpToolProperty { Name = "DestinationLat", Type = "number", Description = "Destination latitude.", Required = true },
            new McpToolProperty { Name = "DestinationLon", Type = "number", Description = "Destination longitude.", Required = true },
            new McpToolProperty { Name = "AvoidBufferMeters", Type = "number", Description = "Buffer distance in meters around fire perimeters to avoid. Default is 2000.", Required = false },
            new McpToolProperty { Name = "DepartAtIsoUtc", Type = "string", Description = "Optional departure time in ISO 8601 UTC format (e.g., 2023-08-01T15:30:00Z).", Required = false },
            new McpToolProperty { Name = "Profile", Type = "string", Description = "Routing profile (e.g., driving, walking). Default is driving.", Required = false }
        };
    }

    public sealed class RoutingFireAwareShortestRequest
    {
        public required double OriginLat { get; set; }
        public required double OriginLon { get; set; }
        public required double DestinationLat { get; set; }
        public required double DestinationLon { get; set; }
        public double? AvoidBufferMeters { get; set; }
        public string? DepartAtIsoUtc { get; set; }
        public string? Profile { get; set; } = "driving";
    }
}