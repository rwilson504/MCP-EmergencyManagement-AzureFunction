using Azure.Storage.Blobs;
using Azure.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System.Text;

namespace EmergencyManagementMCP.Services
{
    public class GeoJsonCache : IGeoJsonCache
    {
        private readonly BlobServiceClient _blobServiceClient;
        private readonly ILogger<GeoJsonCache> _logger;
        private readonly string _containerName;

        public GeoJsonCache(ILogger<GeoJsonCache> logger, IConfiguration config)
        {
            _logger = logger;
            _containerName = config["Storage:CacheContainer"] ?? "routing-cache";
            
            var blobServiceUrl = config["Storage:BlobServiceUrl"];
            if (string.IsNullOrEmpty(blobServiceUrl))
            {
                throw new InvalidOperationException("Storage:BlobServiceUrl configuration is required");
            }

            // Use DefaultAzureCredential for managed identity authentication
            _blobServiceClient = new BlobServiceClient(new Uri(blobServiceUrl), new DefaultAzureCredential());
            
            _logger.LogInformation("GeoJsonCache initialized with container: {ContainerName}", _containerName);
        }

        public async Task<string> LoadOrRefreshAsync(string key, TimeSpan ttl, Func<Task<string>> refresher)
        {
            try
            {
                var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
                var blobClient = containerClient.GetBlobClient($"{key}.json");

                // Check if blob exists and is within TTL
                if (await blobClient.ExistsAsync())
                {
                    var properties = await blobClient.GetPropertiesAsync();
                    var lastModified = properties.Value.LastModified;
                    
                    if (DateTimeOffset.UtcNow - lastModified < ttl)
                    {
                        _logger.LogInformation("Cache hit for key: {Key}, age: {Age}", key, DateTimeOffset.UtcNow - lastModified);
                        
                        var downloadResult = await blobClient.DownloadContentAsync();
                        return downloadResult.Value.Content.ToString();
                    }
                    else
                    {
                        _logger.LogInformation("Cache expired for key: {Key}, age: {Age}", key, DateTimeOffset.UtcNow - lastModified);
                    }
                }
                else
                {
                    _logger.LogInformation("Cache miss for key: {Key}", key);
                }

                // Cache miss or expired - refresh data
                var freshData = await refresher();
                
                // Store in cache
                using var stream = new MemoryStream(Encoding.UTF8.GetBytes(freshData));
                await blobClient.UploadAsync(stream, overwrite: true);
                
                _logger.LogInformation("Cached fresh data for key: {Key}, size: {Size} bytes", key, freshData.Length);
                
                return freshData;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in cache operation for key: {Key}", key);
                
                // Fall back to refresher if cache fails
                try
                {
                    return await refresher();
                }
                catch (Exception refreshEx)
                {
                    _logger.LogError(refreshEx, "Refresher also failed for key: {Key}", key);
                    throw;
                }
            }
        }
    }
}