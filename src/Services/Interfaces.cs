using EmergencyManagementMCP.Models;

namespace EmergencyManagementMCP.Services
{
    public interface IGeoServiceClient
    {
        Task<string> FetchPerimetersAsGeoJsonAsync(BoundingBox bbox, int sinceMins = 60);
        Task<List<AvoidRectangle>> TryFetchClosureRectanglesAsync(BoundingBox bbox);
    }

    public interface IGeoJsonCache
    {
        Task<string> LoadOrRefreshAsync(string key, TimeSpan ttl, Func<Task<string>> refresher);
    }

    public interface IGeometryUtils
    {
        BoundingBox ComputeBBox(Coordinate origin, Coordinate destination, double bufferKm);
        List<AvoidRectangle> BuildAvoidRectanglesFromGeoJson(string geoJson, double bufferKm, int maxRects = 10);
    }

    public interface IRouterClient
    {
        Task<RouteResult> GetRouteAsync(Coordinate origin, Coordinate destination, List<AvoidRectangle> avoidAreas, DateTime? departAt = null);
    }
}