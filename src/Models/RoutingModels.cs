using System.Text.Json.Serialization;

namespace EmergencyManagementMCP.Models
{
    // Existing models...
    public class BoundingBox
    {
        public double MinLat { get; set; }
        public double MinLon { get; set; }
        public double MaxLat { get; set; }
        public double MaxLon { get; set; }
    }

    public class Coordinate
    {
        public double Lat { get; set; }
        public double Lon { get; set; }
    }

    public class AvoidRectangle
    {
        [JsonPropertyName("minLon")]
        public double MinLon { get; set; }
        
        [JsonPropertyName("minLat")]
        public double MinLat { get; set; }
        
        [JsonPropertyName("maxLon")]
        public double MaxLon { get; set; }
        
        [JsonPropertyName("maxLat")]
        public double MaxLat { get; set; }

        public override string ToString()
        {
            return $"{MinLon},{MinLat},{MaxLon},{MaxLat}";
        }
    }

    // Route Link API models
    public class RouteSpec
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = "FeatureCollection";
        
        [JsonPropertyName("features")]
        public RouteFeature[] Features { get; set; } = Array.Empty<RouteFeature>();
        
        [JsonPropertyName("travelMode")]
        public string TravelMode { get; set; } = "driving";
        
        [JsonPropertyName("routeOutputOptions")]
        public string[] RouteOutputOptions { get; set; } = new[] { "routePath" };
        
        [JsonPropertyName("avoidAreas")]
        public MultiPolygon? AvoidAreas { get; set; }
        
        [JsonPropertyName("ttlMinutes")]
        public int? TtlMinutes { get; set; }
    }

    public class RouteFeature
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = "Feature";
        
        [JsonPropertyName("geometry")]
        public PointGeometry Geometry { get; set; } = new();
        
        [JsonPropertyName("properties")]
        public RouteFeatureProperties Properties { get; set; } = new();
    }

    public class PointGeometry
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = "Point";
        
        [JsonPropertyName("coordinates")]
        public double[] Coordinates { get; set; } = Array.Empty<double>();
    }

    public class RouteFeatureProperties
    {
        [JsonPropertyName("pointIndex")]
        public int PointIndex { get; set; }
        
        [JsonPropertyName("pointType")]
        public string PointType { get; set; } = "waypoint";
    }

    public class MultiPolygon
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = "MultiPolygon";
        
        [JsonPropertyName("coordinates")]
        public double[][][][] Coordinates { get; set; } = Array.Empty<double[][][]>();
    }

    public class RouteLink
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;
        
        [JsonPropertyName("url")]
        public string Url { get; set; } = string.Empty;
        
        [JsonPropertyName("createdAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        [JsonPropertyName("expiresAt")]
        public DateTime? ExpiresAt { get; set; }
    }

    public class RouteLinkData
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;
        
        [JsonPropertyName("sasUrl")]
        public string SasUrl { get; set; } = string.Empty;
        
        [JsonPropertyName("createdAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        [JsonPropertyName("expiresAt")]
        public DateTime? ExpiresAt { get; set; }
        
        [JsonPropertyName("azureMapsPostData")]
        public AzureMapsPostData? AzureMapsPostData { get; set; }
    }

    public class AzureMapsPostData
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = "FeatureCollection";
        
        [JsonPropertyName("features")]
        public RouteFeature[] Features { get; set; } = Array.Empty<RouteFeature>();
        
        [JsonPropertyName("avoidAreas")]
        public MultiPolygon? AvoidAreas { get; set; }
        
        [JsonPropertyName("routeOutputOptions")]
        public string[] RouteOutputOptions { get; set; } = new[] { "routePath", "itinerary" };
        
        [JsonPropertyName("travelMode")]
        public string TravelMode { get; set; } = "driving";
    }

    // Existing routing models continue below...
    public class DrivingInstruction
    {
        [JsonPropertyName("routeOffsetInMeters")]
        public int RouteOffsetInMeters { get; set; }
        
        [JsonPropertyName("travelTimeInSeconds")]
        public int TravelTimeInSeconds { get; set; }
        
        [JsonPropertyName("point")]
        public Coordinate Point { get; set; } = new();
        
        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;
    }

    public class RouteResult
    {
        [JsonPropertyName("distanceMeters")]
        public int DistanceMeters { get; set; }
        
        [JsonPropertyName("travelTimeSeconds")]
        public int TravelTimeSeconds { get; set; }
        
        // [JsonPropertyName("polylineGeoJson")]
        // public string PolylineGeoJson { get; set; } = string.Empty;
        
        [JsonPropertyName("drivingDirections")]
        public DrivingInstruction[] DrivingDirections { get; set; } = Array.Empty<DrivingInstruction>();
    }

    /// <summary>
    /// Wrapper class that contains both the route calculation result and the Azure Maps POST request JSON
    /// that was used to generate it. This enables the MapPage.tsx to reuse the exact same API request.
    /// </summary>
    public class RouteWithRequestData
    {
        [JsonPropertyName("route")]
        public RouteResult Route { get; set; } = new();
        
        [JsonPropertyName("azureMapsPostData")]
        public AzureMapsPostData AzureMapsPostData { get; set; } = new();
        
        [JsonPropertyName("azureMapsPostJson")]
        public string AzureMapsPostJson { get; set; } = string.Empty;
    }

    public class FireAwareRouteResponse
    {
        [JsonPropertyName("route")]
        public RouteResult Route { get; set; } = new();
        
        [JsonPropertyName("appliedAvoids")]
        public string[] AppliedAvoids { get; set; } = Array.Empty<string>();
        
        [JsonPropertyName("traceId")]
        public string TraceId { get; set; } = string.Empty;

        [JsonPropertyName("shareLink")] 
        public RouteLink? ShareLink { get; set; }

        [JsonPropertyName("envelope")]
        public ResponseEnvelope Envelope { get; set; } = new();
    }

    public class GeocodingResult
    {
        [JsonPropertyName("address")]
        public string Address { get; set; } = string.Empty;
        
        [JsonPropertyName("coordinates")]
        public Coordinate Coordinates { get; set; } = new();
        
        [JsonPropertyName("confidence")]
        public string Confidence { get; set; } = string.Empty;
        
        [JsonPropertyName("formattedAddress")]
        public string FormattedAddress { get; set; } = string.Empty;
    }

    public class FireZoneInfo
    {
        [JsonPropertyName("isInFireZone")]
        public bool IsInFireZone { get; set; }
        
        [JsonPropertyName("fireZoneName")]
        public string FireZoneName { get; set; } = string.Empty;
        
        [JsonPropertyName("incidentName")]
        public string IncidentName { get; set; } = string.Empty;
        
        [JsonPropertyName("containmentPercent")]
        public double? ContainmentPercent { get; set; }
        
        [JsonPropertyName("acresBurned")]
        public double? AcresBurned { get; set; }
        
        [JsonPropertyName("lastUpdate")]
        public DateTime? LastUpdate { get; set; }
    }

    public class AddressFireZoneResponse
    {
        [JsonPropertyName("geocoding")]
        public GeocodingResult Geocoding { get; set; } = new();
        
        [JsonPropertyName("fireZone")]
        public FireZoneInfo FireZone { get; set; } = new();
        
        [JsonPropertyName("traceId")]
        public string TraceId { get; set; } = string.Empty;
    }

    public class CoordinateFireZoneResponse
    {
        [JsonPropertyName("coordinates")]
        public Coordinate Coordinates { get; set; } = new();
        
        [JsonPropertyName("fireZone")]
        public FireZoneInfo FireZone { get; set; } = new();
        
        [JsonPropertyName("traceId")]
        public string TraceId { get; set; } = string.Empty;
    }

    public class AddressRouteResponse
    {
        [JsonPropertyName("originGeocoding")]
        public GeocodingResult OriginGeocoding { get; set; } = new();
        
        [JsonPropertyName("destinationGeocoding")]
        public GeocodingResult DestinationGeocoding { get; set; } = new();
        
        [JsonPropertyName("route")]
        public RouteResult Route { get; set; } = new();
        
        [JsonPropertyName("appliedAvoids")]
        public string[] AppliedAvoids { get; set; } = Array.Empty<string>();
        
        [JsonPropertyName("traceId")]
        public string TraceId { get; set; } = string.Empty;

        [JsonPropertyName("shareLink")]
        public RouteLink? ShareLink { get; set; }

        [JsonPropertyName("envelope")]
        public ResponseEnvelope Envelope { get; set; } = new();
    }

    public class ResponseEnvelope
    {
        [JsonPropertyName("toolVersion")]
        public string ToolVersion { get; set; } = "1.0.0";

        [JsonPropertyName("generatedAtUtc")]
        public DateTime GeneratedAtUtc { get; set; } = DateTime.UtcNow;

        [JsonPropertyName("latencyMs")]
        public long LatencyMs { get; set; }

        [JsonPropertyName("status")]
        public string Status { get; set; } = "ok";
    }
}