using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.Identity;
using Azure.Storage.Blobs;
using EmergencyManagementMCP.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace EmergencyManagementMCP.Services
{
    public class RouteLinkService : IRouteLinkService
    {
        private readonly ILogger<RouteLinkService> _logger;
        private readonly IConfiguration _config;
    private readonly Azure.Core.TokenCredential _credential;
        private readonly string _storageUrl;

        public RouteLinkService(ILogger<RouteLinkService> logger, IConfiguration config)
        {
            _logger = logger;
            _config = config;
            var miClientId = _config["ManagedIdentity:ClientId"] ?? _config["ManagedIdentity:ClientId"] ?? _config["AzureWebJobsStorage:clientId"];
            if (!string.IsNullOrEmpty(miClientId))
            {
                _credential = new ManagedIdentityCredential(miClientId);
                _logger.LogInformation("[RouteLink] Using ManagedIdentityCredential with clientId={ClientId}", miClientId);
            }
            else
            {
                _credential = new DefaultAzureCredential();
                _logger.LogInformation("[RouteLink] Using DefaultAzureCredential (no explicit managed identity clientId)");
            }
            _storageUrl = _config["Storage:BlobServiceUrl"] ?? throw new InvalidOperationException("Storage:BlobServiceUrl missing");
        }

        public async Task<RouteLink> CreateAsync(Coordinate origin, Coordinate destination, IEnumerable<string> appliedAvoids, TimeSpan? ttl = null, CancellationToken cancellationToken = default)
        {
            var startTs = DateTime.UtcNow;
            var avoids = appliedAvoids?.OrderBy(a => a).ToArray() ?? Array.Empty<string>();
            var ttlMinutes = ttl?.TotalMinutes;
            try
            {
                var storageHost = string.Empty;
                try
                {
                    storageHost = new Uri(_storageUrl).Host;
                }
                catch { /* ignore */ }
                _logger.LogInformation("[RouteLink] Begin CreateAsync origin=({OriginLat},{OriginLon}) destination=({DestLat},{DestLon}) avoids={AvoidCount} ttlMinutes={Ttl} storageHost={Host}",
                    origin.Lat, origin.Lon, destination.Lat, destination.Lon, avoids.Length, ttlMinutes, storageHost);

                // Deterministic ID: SHA256 of origin|destination|avoids|day bucket|version
                // Add version suffix to prevent reuse of old blobs with incomplete data  
                var dayBucket = DateTime.UtcNow.ToString("yyyyMMdd");
                var version = "v2"; // Increment if blob format changes
                var input = $"{origin.Lat:F5},{origin.Lon:F5}|{destination.Lat:F5},{destination.Lon:F5}|{string.Join(';', avoids)}|{dayBucket}|{version}";
                string id;
                using (var sha = SHA256.Create())
                {
                    var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
                    id = Convert.ToHexString(hash)[..12].ToLowerInvariant();
                }
                _logger.LogDebug("[RouteLink] Deterministic ID computed {Id} (inputLength={InputLength})", id, input.Length);

                var blobService = new BlobServiceClient(new Uri(_storageUrl), _credential);
                _logger.LogDebug("[RouteLink] BlobServiceClient created using credential={CredType}", _credential.GetType().Name);
                var container = blobService.GetBlobContainerClient("links");
                await container.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
                _logger.LogDebug("[RouteLink] Ensured container 'links' exists");
                var blob = container.GetBlobClient(id + ".json");

                TimeSpan effectiveTtl = ttl ?? TimeSpan.FromMinutes(1440); // default 24h
                DateTime expiresAt = DateTime.UtcNow.Add(effectiveTtl);
                _logger.LogDebug("[RouteLink] Effective TTL minutes {TtlMinutes} expiresAt={ExpiresAt:O}", effectiveTtl.TotalMinutes, expiresAt);

                var existed = await blob.ExistsAsync(cancellationToken);
                if (!existed)
                {
                    // Create proper RouteSpec format expected by RouteLinksFunction and Map Page
                    var routeSpec = new RouteSpec
                    {
                        Type = "FeatureCollection",
                        Features = new[]
                        {
                            new RouteFeature
                            {
                                Type = "Feature",
                                Geometry = new PointGeometry
                                {
                                    Type = "Point",
                                    Coordinates = new[] { origin.Lon, origin.Lat } // GeoJSON format: [lon, lat]
                                },
                                Properties = new RouteFeatureProperties
                                {
                                    PointIndex = 0,
                                    PointType = "waypoint"
                                }
                            },
                            new RouteFeature
                            {
                                Type = "Feature", 
                                Geometry = new PointGeometry
                                {
                                    Type = "Point",
                                    Coordinates = new[] { destination.Lon, destination.Lat } // GeoJSON format: [lon, lat]
                                },
                                Properties = new RouteFeatureProperties
                                {
                                    PointIndex = 1,
                                    PointType = "waypoint"
                                }
                            }
                        },
                        TravelMode = "driving",
                        RouteOutputOptions = new[] { "routePath", "itinerary" },
                        TtlMinutes = ttl.HasValue ? (int)ttl.Value.TotalMinutes : null
                        // Note: AvoidAreas could be populated here if avoids contained area data,
                        // but currently avoids appears to be string identifiers rather than geometry
                    };
                    
                    var json = JsonSerializer.Serialize(routeSpec);
                    using var ms = new MemoryStream(Encoding.UTF8.GetBytes(json));
                    await blob.UploadAsync(ms, overwrite: true, cancellationToken);
                    await blob.SetMetadataAsync(new Dictionary<string, string> { ["ExpiresAt"] = expiresAt.ToString("O") }, cancellationToken: cancellationToken);
                    _logger.LogInformation("[RouteLink] Created new deterministic route link {Id}", id);
                }
                else
                {
                    _logger.LogInformation("[RouteLink] Reusing existing deterministic route link {Id}", id);
                }

                // Build a full absolute URL when possible.
                // Priority order:
                // 1. Explicit configuration RouteLinks:BaseUrl (can be https://myapp.example)
                // 3. Relative path fallback ("/view?id=<id>")
                var configuredBase = _config["RouteLinks:BaseUrl"];
                string url;
                if (!string.IsNullOrWhiteSpace(configuredBase))
                {
                    var norm = configuredBase.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? configuredBase : $"https://{configuredBase}";
                    url = $"{norm.TrimEnd('/')}/view?id={id}";
                }                
                else
                {
                    url = $"/view?id={id}"; // relative fallback for local dev
                }
                _logger.LogDebug("[RouteLink] Resolved URL {Url} (configuredBase={ConfiguredBase})", url, configuredBase);

                var elapsedMs = (DateTime.UtcNow - startTs).TotalMilliseconds;
                _logger.LogInformation("[RouteLink] Completed CreateAsync id={Id} elapsedMs={ElapsedMs}", id, elapsedMs);

                return new RouteLink
                {
                    Id = id,
                    Url = url,
                    CreatedAt = DateTime.UtcNow,
                    ExpiresAt = expiresAt
                };
            }
            catch (Exception ex)
            {
                var elapsedMs = (DateTime.UtcNow - startTs).TotalMilliseconds;
                _logger.LogError(ex, "[RouteLink] Failed CreateAsync origin=({OriginLat},{OriginLon}) destination=({DestLat},{DestLon}) avoids={AvoidCount} elapsedMs={ElapsedMs}",
                    origin.Lat, origin.Lon, destination.Lat, destination.Lon, avoids.Length, elapsedMs);
                throw; // propagate so caller can decide fallback
            }
        }

        public async Task<RouteLink> CreateAsync(Coordinate origin, Coordinate destination, IEnumerable<string> appliedAvoids, string azureMapsPostJson, TimeSpan? ttl = null, CancellationToken cancellationToken = default)
        {
            var startTs = DateTime.UtcNow;
            var avoids = appliedAvoids?.OrderBy(a => a).ToArray() ?? Array.Empty<string>();
            var ttlMinutes = ttl?.TotalMinutes;
            try
            {
                var storageHost = string.Empty;
                try
                {
                    storageHost = new Uri(_storageUrl).Host;
                }
                catch { /* ignore */ }
                _logger.LogInformation("[RouteLink] Begin CreateAsync with Azure Maps POST JSON: origin=({OriginLat},{OriginLon}) destination=({DestLat},{DestLon}) avoids={AvoidCount} ttlMinutes={Ttl} storageHost={Host}",
                    origin.Lat, origin.Lon, destination.Lat, destination.Lon, avoids.Length, ttlMinutes, storageHost);

                // Deterministic ID: SHA256 of origin|destination|avoids|day bucket|version
                // Add version suffix to prevent reuse of old blobs with incomplete data
                var dayBucket = DateTime.UtcNow.ToString("yyyyMMdd");
                var version = "v2"; // Increment if blob format changes
                var input = $"{origin.Lat:F5},{origin.Lon:F5}|{destination.Lat:F5},{destination.Lon:F5}|{string.Join(';', avoids)}|{dayBucket}|{version}";
                string id;
                using (var sha = SHA256.Create())
                {
                    var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
                    id = Convert.ToHexString(hash)[..12].ToLowerInvariant();
                }
                _logger.LogDebug("[RouteLink] Deterministic ID computed {Id} (inputLength={InputLength})", id, input.Length);

                var blobService = new BlobServiceClient(new Uri(_storageUrl), _credential);
                _logger.LogDebug("[RouteLink] BlobServiceClient created using credential={CredType}", _credential.GetType().Name);
                var container = blobService.GetBlobContainerClient("links");
                await container.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
                _logger.LogDebug("[RouteLink] Ensured container 'links' exists");
                var blob = container.GetBlobClient(id + ".json");

                TimeSpan effectiveTtl = ttl ?? TimeSpan.FromMinutes(1440); // default 24h
                DateTime expiresAt = DateTime.UtcNow.Add(effectiveTtl);
                _logger.LogDebug("[RouteLink] Effective TTL minutes {TtlMinutes} expiresAt={ExpiresAt:O}", effectiveTtl.TotalMinutes, expiresAt);

                var existed = await blob.ExistsAsync(cancellationToken);
                if (!existed)
                {
                    // Parse the Azure Maps POST JSON and add TTL to it for storage
                    var postData = JsonSerializer.Deserialize<JsonElement>(azureMapsPostJson);
                    
                    // Create a new object with the TTL added for storage
                    var storageData = new Dictionary<string, object?>
                    {
                        ["type"] = postData.GetProperty("type").GetString(),
                        ["features"] = JsonSerializer.Deserialize<object>(postData.GetProperty("features").GetRawText()),
                        ["travelMode"] = postData.GetProperty("travelMode").GetString(),
                        ["routeOutputOptions"] = JsonSerializer.Deserialize<string[]>(postData.GetProperty("routeOutputOptions").GetRawText()),
                        ["ttlMinutes"] = ttl.HasValue ? (int)ttl.Value.TotalMinutes : null
                    };
                    
                    // Add avoidAreas if it exists
                    if (postData.TryGetProperty("avoidAreas", out var avoidAreasElement) && avoidAreasElement.ValueKind != JsonValueKind.Null)
                    {
                        storageData["avoidAreas"] = JsonSerializer.Deserialize<object>(avoidAreasElement.GetRawText());
                    }
                    
                    var storageJson = JsonSerializer.Serialize(storageData, new JsonSerializerOptions 
                    { 
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                    });
                    
                    using var ms = new MemoryStream(Encoding.UTF8.GetBytes(storageJson));
                    await blob.UploadAsync(ms, overwrite: true, cancellationToken);
                    await blob.SetMetadataAsync(new Dictionary<string, string> { ["ExpiresAt"] = expiresAt.ToString("O") }, cancellationToken: cancellationToken);
                    _logger.LogInformation("[RouteLink] Created new deterministic route link with Azure Maps POST JSON {Id}", id);
                }
                else
                {
                    _logger.LogInformation("[RouteLink] Reusing existing deterministic route link {Id}", id);
                }

                // Build a full absolute URL when possible.
                // Priority order:
                // 1. Explicit configuration RouteLinks:BaseUrl (can be https://myapp.example)
                // 3. Relative path fallback ("/view?id=<id>")
                var configuredBase = _config["RouteLinks:BaseUrl"];
                string url;
                if (!string.IsNullOrWhiteSpace(configuredBase))
                {
                    var norm = configuredBase.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? configuredBase : $"https://{configuredBase}";
                    url = $"{norm.TrimEnd('/')}/view?id={id}";
                }                
                else
                {
                    url = $"/view?id={id}"; // relative fallback for local dev
                }
                _logger.LogDebug("[RouteLink] Resolved URL {Url} (configuredBase={ConfiguredBase})", url, configuredBase);

                var elapsedMs = (DateTime.UtcNow - startTs).TotalMilliseconds;
                _logger.LogInformation("[RouteLink] Completed CreateAsync with Azure Maps POST JSON: id={Id} elapsedMs={ElapsedMs}", id, elapsedMs);

                return new RouteLink
                {
                    Id = id,
                    Url = url,
                    CreatedAt = DateTime.UtcNow,
                    ExpiresAt = expiresAt
                };
            }
            catch (Exception ex)
            {
                var elapsedMs = (DateTime.UtcNow - startTs).TotalMilliseconds;
                _logger.LogError(ex, "[RouteLink] Failed CreateAsync with Azure Maps POST JSON: origin=({OriginLat},{OriginLon}) destination=({DestLat},{DestLon}) avoids={AvoidCount} elapsedMs={ElapsedMs}",
                    origin.Lat, origin.Lon, destination.Lat, destination.Lon, avoids.Length, elapsedMs);
                throw; // propagate so caller can decide fallback
            }
        }
    }
}