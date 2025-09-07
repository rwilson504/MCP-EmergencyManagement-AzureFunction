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

            // Log all config values for diagnostics
            // foreach (var kvp in config.AsEnumerable())
            // {
            //     if (!string.IsNullOrEmpty(kvp.Value))
            //     {
            //         _logger.LogInformation("Config: {Key} = {Value}", kvp.Key, kvp.Value);
            //     }
            // }

            var blobServiceUrl = config["Storage:BlobServiceUrl"];
            if (string.IsNullOrEmpty(blobServiceUrl))
            {
                _logger.LogError("Storage:BlobServiceUrl configuration is missing");
                throw new InvalidOperationException("Storage:BlobServiceUrl configuration is required");
            }

            try
            {
                // Use DefaultAzureCredential for managed identity authentication
                _blobServiceClient = new BlobServiceClient(new Uri(blobServiceUrl), new DefaultAzureCredential());
                _logger.LogInformation("GeoJsonCache initialized successfully: container={ContainerName}, blobService={BlobServiceUrl}", 
                    _containerName, blobServiceUrl);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize GeoJsonCache with blob service URL: {BlobServiceUrl}", blobServiceUrl);
                throw;
            }
        }

        public async Task<string> LoadOrRefreshAsync(string key, TimeSpan ttl, Func<Task<string>> refresher)
        {
            var requestId = Guid.NewGuid().ToString("N")[..8];
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            _logger.LogDebug("Starting cache operation: key={Key}, ttl={TTL}, requestId={RequestId}", 
                key, ttl, requestId);
                
            try
            {
                var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
                var blobClient = containerClient.GetBlobClient($"{key}.json");

                _logger.LogDebug("Checking cache existence for blob: {BlobName}, requestId={RequestId}", 
                    $"{key}.json", requestId);

                // Check if blob exists and is within TTL
                if (await blobClient.ExistsAsync())
                {
                    _logger.LogDebug("Cache blob exists, checking TTL, requestId={RequestId}", requestId);
                    
                    var properties = await blobClient.GetPropertiesAsync();
                    var lastModified = properties.Value.LastModified;
                    var age = DateTimeOffset.UtcNow - lastModified;
                    
                    if (age < ttl)
                    {
                        stopwatch.Stop();
                        _logger.LogInformation("Cache hit: key={Key}, age={Age}, elapsed={ElapsedMs}ms, requestId={RequestId}", 
                            key, age, stopwatch.ElapsedMilliseconds, requestId);
                        
                        var downloadResult = await blobClient.DownloadContentAsync();
                        var content = downloadResult.Value.Content.ToString();
                        
                        _logger.LogDebug("Downloaded cached content: {Size} chars, requestId={RequestId}", 
                            content.Length, requestId);
                            
                        return content;
                    }
                    else
                    {
                        _logger.LogInformation("Cache expired: key={Key}, age={Age}, ttl={TTL}, requestId={RequestId}", 
                            key, age, ttl, requestId);
                    }
                }
                else
                {
                    _logger.LogInformation("Cache miss: key={Key}, requestId={RequestId}", key, requestId);
                }

                // Cache miss or expired - refresh data
                _logger.LogDebug("Calling refresher function, requestId={RequestId}", requestId);
                var refreshStopwatch = System.Diagnostics.Stopwatch.StartNew();
                
                var freshData = await refresher();
                
                refreshStopwatch.Stop();
                _logger.LogDebug("Refresher completed: {Size} chars, elapsed={ElapsedMs}ms, requestId={RequestId}", 
                    freshData.Length, refreshStopwatch.ElapsedMilliseconds, requestId);
                
                // Store in cache
                _logger.LogDebug("Storing fresh data in cache, requestId={RequestId}", requestId);
                using var stream = new MemoryStream(Encoding.UTF8.GetBytes(freshData));
                await blobClient.UploadAsync(stream, overwrite: true);
                
                stopwatch.Stop();
                _logger.LogInformation("Cached fresh data: key={Key}, size={Size} chars, elapsed={ElapsedMs}ms, requestId={RequestId}", 
                    key, freshData.Length, stopwatch.ElapsedMilliseconds, requestId);
                
                return freshData;
            }
            catch (Azure.RequestFailedException azureEx)
            {
                stopwatch.Stop();
                _logger.LogError(azureEx, "Azure storage error in cache operation: key={Key}, statusCode={StatusCode}, elapsed={ElapsedMs}ms, requestId={RequestId}", 
                    key, azureEx.Status, stopwatch.ElapsedMilliseconds, requestId);
                
                // Fall back to refresher if cache fails
                try
                {
                    _logger.LogWarning("Falling back to refresher due to cache error, requestId={RequestId}", requestId);
                    return await refresher();
                }
                catch (Exception refreshEx)
                {
                    _logger.LogError(refreshEx, "Refresher also failed after cache error: key={Key}, requestId={RequestId}", key, requestId);
                    throw;
                }
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "Unexpected error in cache operation: key={Key}, elapsed={ElapsedMs}ms, requestId={RequestId}", 
                    key, stopwatch.ElapsedMilliseconds, requestId);
                
                // Fall back to refresher if cache fails
                try
                {
                    _logger.LogWarning("Falling back to refresher due to unexpected error, requestId={RequestId}", requestId);
                    return await refresher();
                }
                catch (Exception refreshEx)
                {
                    _logger.LogError(refreshEx, "Refresher also failed after unexpected error: key={Key}, requestId={RequestId}", key, requestId);
                    throw;
                }
            }
        }
    }
}