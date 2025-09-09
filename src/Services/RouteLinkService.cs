using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
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
            var miClientId = _config["ManagedIdentity:ClientId"] ?? _config["ManagedIdentity__ClientId"] ?? _config["AzureWebJobsStorage:clientId"];
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

                // Deterministic ID: SHA256 of origin|destination|avoids|day bucket
                var dayBucket = DateTime.UtcNow.ToString("yyyyMMdd");
                var input = $"{origin.Lat:F5},{origin.Lon:F5}|{destination.Lat:F5},{destination.Lon:F5}|{string.Join(';', avoids)}|{dayBucket}";
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
                    var doc = new
                    {
                        origin,
                        destination,
                        appliedAvoids = avoids,
                        createdAt = DateTime.UtcNow,
                        expiresAt
                    };
                    var json = JsonSerializer.Serialize(doc);
                    using var ms = new MemoryStream(Encoding.UTF8.GetBytes(json));
                    await blob.UploadAsync(ms, overwrite: true, cancellationToken);
                    await blob.SetMetadataAsync(new Dictionary<string, string> { ["ExpiresAt"] = expiresAt.ToString("O") }, cancellationToken: cancellationToken);
                    _logger.LogInformation("[RouteLink] Created new deterministic route link {Id}", id);
                }
                else
                {
                    _logger.LogInformation("[RouteLink] Reusing existing deterministic route link {Id}", id);
                }

                var baseUrl = _config["RouteLinks:BaseUrl"]; // optional override
                var url = string.IsNullOrEmpty(baseUrl) ? $"/view?id={id}" : $"{baseUrl.TrimEnd('/')}/view?id={id}";
                _logger.LogDebug("[RouteLink] Resolved URL {Url} (baseUrl={BaseUrl})", url, baseUrl);

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
    }
}