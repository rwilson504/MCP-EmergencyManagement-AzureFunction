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
            
            _logger.LogInformation("Received fire-aware routing request: origin=({OriginLat},{OriginLon}), destination=({DestLat},{DestLon}), bufferKm={BufferKm}, useClosures={UseClosures}, traceId={TraceId}",
                request.origin.lat, request.origin.lon, request.destination.lat, request.destination.lon, request.bufferKm, request.useClosures, traceId);

            try
            {
                var origin = new Coordinate { Lat = request.origin.lat, Lon = request.origin.lon };
                var destination = new Coordinate { Lat = request.destination.lat, Lon = request.destination.lon };

                // 1. Compute bounding box with buffer
                var bbox = _geometryUtils.ComputeBBox(origin, destination, request.bufferKm);

                // 2. Cache key for fire perimeters
                var cacheKey = $"fire-perimeters-{bbox.MinLat:F3}-{bbox.MinLon:F3}-{bbox.MaxLat:F3}-{bbox.MaxLon:F3}";

                // 3. Load or refresh fire GeoJSON data (with 10-minute TTL)
                var fireGeoJson = await _geoJsonCache.LoadOrRefreshAsync(
                    cacheKey,
                    TimeSpan.FromMinutes(10),
                    () => _geoServiceClient.FetchPerimetersAsGeoJsonAsync(bbox, 60));

                // 4. Build avoid rectangles from fire perimeters (cap at 10)
                var avoidRectangles = _geometryUtils.BuildAvoidRectanglesFromGeoJson(fireGeoJson, request.bufferKm, 10);

                // 5. Optionally add closure rectangles
                if (request.useClosures)
                {
                    var closureRectangles = await _geoServiceClient.TryFetchClosureRectanglesAsync(bbox);
                    avoidRectangles.AddRange(closureRectangles.Take(10 - avoidRectangles.Count));
                }

                // 6. Call router with avoid areas
                DateTime? departAt = null;
                if (!string.IsNullOrEmpty(request.departAtIsoUtc))
                {
                    if (DateTime.TryParse(request.departAtIsoUtc, out var parsedDate))
                    {
                        departAt = parsedDate.ToUniversalTime();
                    }
                }

                var route = await _routerClient.GetRouteAsync(origin, destination, avoidRectangles, departAt);

                // 7. Build response
                var response = new FireAwareRouteResponse
                {
                    Route = route,
                    AppliedAvoids = avoidRectangles.Select(r => r.ToString()).ToArray(),
                    TraceId = traceId
                };

                _logger.LogInformation("Fire-aware route calculated successfully: {Distance}m, {Time}s, {AvoidCount} avoids, traceId={TraceId}",
                    route.DistanceMeters, route.TravelTimeSeconds, avoidRectangles.Count, traceId);

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating fire-aware route, traceId={TraceId}", traceId);
                
                // Return error response
                return new FireAwareRouteResponse
                {
                    Route = new RouteResult { DistanceMeters = 0, TravelTimeSeconds = 0, PolylineGeoJson = "{}" },
                    AppliedAvoids = Array.Empty<string>(),
                    TraceId = traceId
                };
            }
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