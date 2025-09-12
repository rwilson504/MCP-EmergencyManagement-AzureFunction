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
        FireZoneInfo CheckPointInFireZones(string geoJson, Coordinate point);
    }

    public interface IRouterClient
    {
        Task<RouteResult> GetRouteAsync(Coordinate origin, Coordinate destination, List<AvoidRectangle> avoidAreas, DateTime? departAt = null);
        Task<RouteWithRequestData> GetRouteWithRequestDataAsync(Coordinate origin, Coordinate destination, List<AvoidRectangle> avoidAreas, DateTime? departAt = null);
    }

    public interface IRouteLinkService
    {
        /// <summary>
        /// Creates (or reuses deterministically) a shareable route link artifact given core inputs.
        /// </summary>
        /// <param name="origin">Origin coordinate</param>
        /// <param name="destination">Destination coordinate</param>
        /// <param name="appliedAvoids">Applied avoid rectangles (string form)</param>
        /// <param name="ttl">Time to live (null for default)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>RouteLink with id + URL</returns>
        Task<RouteLink> CreateAsync(Coordinate origin, Coordinate destination, IEnumerable<string> appliedAvoids, TimeSpan? ttl = null, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Creates (or reuses deterministically) a shareable route link artifact with Azure Maps request data.
        /// </summary>
        /// <param name="origin">Origin coordinate</param>
        /// <param name="destination">Destination coordinate</param>
        /// <param name="appliedAvoids">Applied avoid rectangles (string form)</param>
        /// <param name="azureMapsPostData">Azure Maps POST request data for MapPage.tsx to reuse</param>
        /// <param name="ttl">Time to live (null for default)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>RouteLink with id + URL</returns>
        Task<RouteLink> CreateAsync(Coordinate origin, Coordinate destination, IEnumerable<string> appliedAvoids, AzureMapsPostData azureMapsPostData, TimeSpan? ttl = null, CancellationToken cancellationToken = default);
    }

    public interface IGeocodingClient
    {
        Task<GeocodingResult> GeocodeAddressAsync(string address);
    }
}