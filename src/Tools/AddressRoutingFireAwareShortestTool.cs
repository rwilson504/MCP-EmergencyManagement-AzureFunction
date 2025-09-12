using EmergencyManagementMCP.Models;
using EmergencyManagementMCP.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;
using Microsoft.Extensions.Logging;

namespace EmergencyManagementMCP.Tools
{
    public class AddressRoutingFireAwareShortestTool
    {
        private readonly IGeocodingClient _geocodingClient;
        private readonly IGeoServiceClient _geoServiceClient;
        private readonly IGeoJsonCache _geoJsonCache;
        private readonly IGeometryUtils _geometryUtils;
        private readonly IRouterClient _routerClient;
    private readonly IRouteLinkService _routeLinkService;
        private readonly ILogger<AddressRoutingFireAwareShortestTool> _logger;

        public AddressRoutingFireAwareShortestTool(
            IGeocodingClient geocodingClient,
            IGeoServiceClient geoServiceClient,
            IGeoJsonCache geoJsonCache,
            IGeometryUtils geometryUtils,
            IRouterClient routerClient,
            IRouteLinkService routeLinkService,
            ILogger<AddressRoutingFireAwareShortestTool> logger)
        {
            _geocodingClient = geocodingClient;
            _geoServiceClient = geoServiceClient;
            _geoJsonCache = geoJsonCache;
            _geometryUtils = geometryUtils;
            _routerClient = routerClient;
            _routeLinkService = routeLinkService;
            _logger = logger;
        }

        [Function(nameof(AddressRoutingFireAwareShortestTool))]
        public async Task<object> Run(
            [McpToolTrigger(ToolName, ToolDescription)] ToolInvocationContext context,
            [McpToolProperty(AddressRoutingFireAwareShortestToolPropertyStrings.OriginAddressName, AddressRoutingFireAwareShortestToolPropertyStrings.OriginAddressType, AddressRoutingFireAwareShortestToolPropertyStrings.OriginAddressDescription, Required = true)] string originAddress,
            [McpToolProperty(AddressRoutingFireAwareShortestToolPropertyStrings.DestinationAddressName, AddressRoutingFireAwareShortestToolPropertyStrings.DestinationAddressType, AddressRoutingFireAwareShortestToolPropertyStrings.DestinationAddressDescription, Required = true)] string destinationAddress,
            [McpToolProperty(AddressRoutingFireAwareShortestToolPropertyStrings.AvoidBufferMetersName, AddressRoutingFireAwareShortestToolPropertyStrings.AvoidBufferMetersType, AddressRoutingFireAwareShortestToolPropertyStrings.AvoidBufferMetersDescription, Required = false)] double? avoidBufferMeters,
            [McpToolProperty(AddressRoutingFireAwareShortestToolPropertyStrings.DepartAtIsoUtcName, AddressRoutingFireAwareShortestToolPropertyStrings.DepartAtIsoUtcType, AddressRoutingFireAwareShortestToolPropertyStrings.DepartAtIsoUtcDescription, Required = false)] string? departAtIsoUtc,
            [McpToolProperty(AddressRoutingFireAwareShortestToolPropertyStrings.ProfileName, AddressRoutingFireAwareShortestToolPropertyStrings.ProfileType, AddressRoutingFireAwareShortestToolPropertyStrings.ProfileDescription, Required = false)] string? profile,
            [McpToolProperty(AddressRoutingFireAwareShortestToolPropertyStrings.PersistShareLinkName, AddressRoutingFireAwareShortestToolPropertyStrings.PersistShareLinkType, AddressRoutingFireAwareShortestToolPropertyStrings.PersistShareLinkDescription, Required = false)] bool? persistShareLink,
            [McpToolProperty(AddressRoutingFireAwareShortestToolPropertyStrings.ShareLinkTtlMinutesName, AddressRoutingFireAwareShortestToolPropertyStrings.ShareLinkTtlMinutesType, AddressRoutingFireAwareShortestToolPropertyStrings.ShareLinkTtlMinutesDescription, Required = false)] int? shareLinkTtlMinutes
        )
        {
            var traceId = Guid.NewGuid().ToString("N")[..8];
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            _logger.LogInformation("Starting address-based fire-aware routing request: originAddress=\"{OriginAddress}\", destinationAddress=\"{DestinationAddress}\", avoidBufferMeters={AvoidBufferMeters}, profile={Profile}, departAtIsoUtc={DepartAtIsoUtc}, traceId={TraceId}",
                originAddress, destinationAddress, avoidBufferMeters, profile, departAtIsoUtc, traceId);

            // Validate input parameters
            if (string.IsNullOrWhiteSpace(originAddress))
            {
                _logger.LogError("Origin address cannot be null or empty, traceId={TraceId}", traceId);
                return CreateErrorResponse("Origin address cannot be null or empty", traceId);
            }

            if (string.IsNullOrWhiteSpace(destinationAddress))
            {
                _logger.LogError("Destination address cannot be null or empty, traceId={TraceId}", traceId);
                return CreateErrorResponse("Destination address cannot be null or empty", traceId);
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
                // Step 1: Geocode origin address
                _logger.LogDebug("Step 1: Geocoding origin address, traceId={TraceId}", traceId);
                var stepStopwatch = System.Diagnostics.Stopwatch.StartNew();
                
                var originGeocodingResult = await _geocodingClient.GeocodeAddressAsync(originAddress);
                
                stepStopwatch.Stop();
                _logger.LogDebug("Step 1 completed in {ElapsedMs}ms: origin geocoded to ({Lat},{Lon}), traceId={TraceId}", 
                    stepStopwatch.ElapsedMilliseconds, originGeocodingResult.Coordinates.Lat, originGeocodingResult.Coordinates.Lon, traceId);

                // Step 2: Geocode destination address
                _logger.LogDebug("Step 2: Geocoding destination address, traceId={TraceId}", traceId);
                stepStopwatch.Restart();
                
                var destinationGeocodingResult = await _geocodingClient.GeocodeAddressAsync(destinationAddress);
                
                stepStopwatch.Stop();
                _logger.LogDebug("Step 2 completed in {ElapsedMs}ms: destination geocoded to ({Lat},{Lon}), traceId={TraceId}", 
                    stepStopwatch.ElapsedMilliseconds, destinationGeocodingResult.Coordinates.Lat, destinationGeocodingResult.Coordinates.Lon, traceId);

                var origin = originGeocodingResult.Coordinates;
                var destination = destinationGeocodingResult.Coordinates;

                // Step 3: Compute bounding box with buffer
                _logger.LogDebug("Step 3: Computing bounding box, traceId={TraceId}", traceId);
                stepStopwatch.Restart();
                
                var bbox = _geometryUtils.ComputeBBox(origin, destination, bufferKm);
                
                stepStopwatch.Stop();
                _logger.LogDebug("Step 3 completed in {ElapsedMs}ms: bbox computed, traceId={TraceId}", 
                    stepStopwatch.ElapsedMilliseconds, traceId);

                // Step 4: Cache key for fire perimeters
                var cacheKey = $"fire-perimeters-{bbox.MinLat:F3}-{bbox.MinLon:F3}-{bbox.MaxLat:F3}-{bbox.MaxLon:F3}";
                _logger.LogDebug("Step 4: Using cache key: {CacheKey}, traceId={TraceId}", cacheKey, traceId);

                // Step 5: Load or refresh fire GeoJSON data (with 10-minute TTL)
                _logger.LogDebug("Step 5: Loading fire perimeter data, traceId={TraceId}", traceId);
                stepStopwatch.Restart();
                
                var fireGeoJson = await _geoJsonCache.LoadOrRefreshAsync(
                    cacheKey,
                    TimeSpan.FromMinutes(10),
                    () => _geoServiceClient.FetchPerimetersAsGeoJsonAsync(bbox, 60));

                stepStopwatch.Stop();
                _logger.LogDebug("Step 5 completed in {ElapsedMs}ms: fire data loaded, size={Size} chars, traceId={TraceId}", 
                    stepStopwatch.ElapsedMilliseconds, fireGeoJson.Length, traceId);

                // Step 6: Build avoid rectangles from fire perimeters (cap at 10)
                _logger.LogDebug("Step 6: Building avoid rectangles from fire data, traceId={TraceId}", traceId);
                stepStopwatch.Restart();
                
                var avoidRectangles = _geometryUtils.BuildAvoidRectanglesFromGeoJson(fireGeoJson, bufferKm, 10);
                
                stepStopwatch.Stop();
                _logger.LogDebug("Step 6 completed in {ElapsedMs}ms: {Count} avoid rectangles built, traceId={TraceId}", 
                    stepStopwatch.ElapsedMilliseconds, avoidRectangles.Count, traceId);

                // Step 7: Optionally add closure rectangles (always included for flat model)
                _logger.LogDebug("Step 7: Fetching closure rectangles, traceId={TraceId}", traceId);
                stepStopwatch.Restart();
                
                var closureRectangles = await _geoServiceClient.TryFetchClosureRectanglesAsync(bbox);
                var availableSlots = Math.Max(0, 10 - avoidRectangles.Count);
                var closuresToAdd = closureRectangles.Take(availableSlots).ToList();
                avoidRectangles.AddRange(closuresToAdd);
                
                stepStopwatch.Stop();
                _logger.LogDebug("Step 7 completed in {ElapsedMs}ms: {ClosureCount} closures added (of {TotalClosures} available), traceId={TraceId}", 
                    stepStopwatch.ElapsedMilliseconds, closuresToAdd.Count, closureRectangles.Count, traceId);

                // Step 8: Profile (not used in routing logic here, but could be passed to router)
                string routingProfile = profile ?? "driving";

                // Step 9: Call router with avoid areas
                _logger.LogDebug("Step 9: Calculating route with {AvoidCount} avoid areas, traceId={TraceId}", 
                    avoidRectangles.Count, traceId);
                stepStopwatch.Restart();
                
                var routeWithData = await _routerClient.GetRouteWithRequestDataAsync(origin, destination, avoidRectangles, departAt);
                var route = routeWithData.Route;

                stepStopwatch.Stop();
                _logger.LogDebug("Step 9 completed in {ElapsedMs}ms: route calculated, traceId={TraceId}", 
                    stepStopwatch.ElapsedMilliseconds, traceId);

                // Step 10: Build response
                var appliedAvoidsArr = avoidRectangles.Select(r => r.ToString()).ToArray();
                RouteLink? shareLink = null;
                if (persistShareLink == true)
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
                        _logger.LogWarning(linkEx, "Failed to create share link (address tool), continuing without it. traceId={TraceId}", traceId);
                    }
                }

                var response = new AddressRouteResponse
                {
                    OriginGeocoding = originGeocodingResult,
                    DestinationGeocoding = destinationGeocodingResult,
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
                _logger.LogInformation("Address-based fire-aware route calculated successfully: originAddress=\"{OriginAddress}\" ({OriginLat},{OriginLon}), destinationAddress=\"{DestinationAddress}\" ({DestinationLat},{DestinationLon}), distance={Distance}m, time={Time}s, avoids={AvoidCount}, totalTime={TotalMs}ms, traceId={TraceId}",
                    originAddress, origin.Lat, origin.Lon, destinationAddress, destination.Lat, destination.Lon, route.DistanceMeters, route.TravelTimeSeconds, avoidRectangles.Count, stopwatch.ElapsedMilliseconds, traceId);

                return response;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "Error calculating address-based fire-aware route after {ElapsedMs}ms, traceId={TraceId}", 
                    stopwatch.ElapsedMilliseconds, traceId);
                
                // Return error response
                return CreateErrorResponse($"Route calculation failed: {ex.Message}", traceId, stopwatch.ElapsedMilliseconds);
            }
        }

        private static object CreateErrorResponse(string errorMessage, string traceId, long? latencyMs = null)
        {
            return new AddressRouteResponse
            {
                OriginGeocoding = new GeocodingResult
                {
                    Address = "ERROR",
                    Coordinates = new Coordinate { Lat = 0, Lon = 0 },
                    FormattedAddress = $"ERROR: {errorMessage}",
                    Confidence = "None"
                },
                DestinationGeocoding = new GeocodingResult
                {
                    Address = "ERROR",
                    Coordinates = new Coordinate { Lat = 0, Lon = 0 },
                    FormattedAddress = $"ERROR: {errorMessage}",
                    Confidence = "None"
                },
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

        public const string ToolName = "routing.addressFireAwareShortest";
        public const string ToolDescription = "Compute the shortest route between addresses while avoiding wildfire perimeters and closures.";
    }
}