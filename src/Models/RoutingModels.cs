using System.Text.Json.Serialization;

namespace EmergencyManagementMCP.Models
{
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
        
        [JsonPropertyName("polylineGeoJson")]
        public string PolylineGeoJson { get; set; } = string.Empty;
        
        [JsonPropertyName("drivingDirections")]
        public DrivingInstruction[] DrivingDirections { get; set; } = Array.Empty<DrivingInstruction>();
    }

    public class FireAwareRouteResponse
    {
        [JsonPropertyName("route")]
        public RouteResult Route { get; set; } = new();
        
        [JsonPropertyName("appliedAvoids")]
        public string[] AppliedAvoids { get; set; } = Array.Empty<string>();
        
        [JsonPropertyName("traceId")]
        public string TraceId { get; set; } = string.Empty;
    }
}