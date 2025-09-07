using EmergencyManagementMCP.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System.Text.Json;
using System.Web;
using Azure.Core;
using Azure.Identity;

namespace EmergencyManagementMCP.Services
{
    public class GeocodingClient : IGeocodingClient
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<GeocodingClient> _logger;
        private readonly TokenCredential _credential;
        private readonly string _searchBase;
        private readonly string? _mapsClientId; // Azure Maps account client ID for x-ms-client-id header

        public GeocodingClient(HttpClient httpClient, ILogger<GeocodingClient> logger, IConfiguration config)
        {
            _httpClient = httpClient;
            _logger = logger;
            _searchBase = config["Maps:SearchBase"] ?? "https://atlas.microsoft.com";
            _mapsClientId = config["Maps:ClientId"];
            
            // Use ManagedIdentityCredential with specific client ID for Azure Functions
            var managedIdentityClientId = config["AzureWebJobsStorage:clientId"];
            
            if (!string.IsNullOrEmpty(managedIdentityClientId))
            {
                _logger.LogDebug("Using ManagedIdentityCredential with client ID: {ClientId}", managedIdentityClientId);
                _credential = new ManagedIdentityCredential(managedIdentityClientId);
            }
            else
            {
                _logger.LogDebug("Using DefaultAzureCredential (no client ID specified)");
                _credential = new DefaultAzureCredential();
            }
            
            _logger.LogInformation("GeocodingClient initialized with Maps API base: {SearchBase}, credential: {CredentialType}", 
                _searchBase, _credential.GetType().Name);
        }

        public async Task<GeocodingResult> GeocodeAddressAsync(string address)
        {
            var requestId = Guid.NewGuid().ToString("N")[..8];
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            _logger.LogInformation("Starting geocoding request: address=\"{Address}\", requestId={RequestId}", 
                address, requestId);

            if (string.IsNullOrWhiteSpace(address))
            {
                _logger.LogError("Address cannot be null or empty, requestId={RequestId}", requestId);
                throw new ArgumentException("Address cannot be null or empty", nameof(address));
            }

            try
            {
                // Get access token for Azure Maps
                var tokenContext = new TokenRequestContext(new[] { "https://atlas.microsoft.com/.default" });
                var tokenResult = await _credential.GetTokenAsync(tokenContext, CancellationToken.None);
                
                _logger.LogDebug("Obtained access token for Azure Maps, expires at: {ExpiresOn}, requestId={RequestId}", 
                    tokenResult.ExpiresOn, requestId);

                var queryParams = new List<string>
                {
                    "api-version=2023-06-01",
                    $"query={HttpUtility.UrlEncode(address)}",
                    "limit=1",
                    "countrySet=US" // Focus on US addresses for emergency management
                };

                var requestUrl = $"{_searchBase}/search/address/json?{string.Join("&", queryParams)}";
                
                _logger.LogDebug("Making Azure Maps Geocoding API request with managed identity authentication, requestId={RequestId}", requestId);

                // Create HTTP request with Authorization header
                using var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tokenResult.Token);
                
                // Add x-ms-client-id header if Azure Maps clientId is present
                if (!string.IsNullOrEmpty(_mapsClientId))
                {
                    request.Headers.Add("x-ms-client-id", _mapsClientId);
                    _logger.LogDebug("Set x-ms-client-id header: {ClientId}", _mapsClientId);
                }

                var httpStopwatch = System.Diagnostics.Stopwatch.StartNew();
                var response = await _httpClient.SendAsync(request);
                httpStopwatch.Stop();
                
                _logger.LogDebug("Azure Maps Geocoding API response received: status={StatusCode}, elapsed={ElapsedMs}ms, requestId={RequestId}",
                    response.StatusCode, httpStopwatch.ElapsedMilliseconds, requestId);
                
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Azure Maps Geocoding API returned error {StatusCode}: {Error}, requestId={RequestId}", 
                        response.StatusCode, errorContent, requestId);
                    throw new HttpRequestException($"Azure Maps Geocoding API error: {response.StatusCode} - {errorContent}");
                }

                var jsonResponse = await response.Content.ReadAsStringAsync();
                
                _logger.LogDebug("Parsing Azure Maps Geocoding response: {Length} chars, requestId={RequestId}", 
                    jsonResponse.Length, requestId);
                
                var geocodingResult = ParseGeocodingResponse(jsonResponse, address, requestId);
                
                stopwatch.Stop();
                _logger.LogInformation("Geocoding completed successfully: address=\"{Address}\", coordinates=({Lat},{Lon}), confidence={Confidence}, elapsed={ElapsedMs}ms, requestId={RequestId}",
                    address, geocodingResult.Coordinates.Lat, geocodingResult.Coordinates.Lon, geocodingResult.Confidence, stopwatch.ElapsedMilliseconds, requestId);
                
                return geocodingResult;
            }
            catch (HttpRequestException httpEx)
            {
                stopwatch.Stop();
                _logger.LogError(httpEx, "HTTP error calling Azure Maps Geocoding API after {ElapsedMs}ms, requestId={RequestId}", 
                    stopwatch.ElapsedMilliseconds, requestId);
                throw;
            }
            catch (TaskCanceledException timeoutEx)
            {
                stopwatch.Stop();
                _logger.LogError(timeoutEx, "Timeout calling Azure Maps Geocoding API after {ElapsedMs}ms, requestId={RequestId}", 
                    stopwatch.ElapsedMilliseconds, requestId);
                throw;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "Unexpected error calling Azure Maps Geocoding API after {ElapsedMs}ms, requestId={RequestId}", 
                    stopwatch.ElapsedMilliseconds, requestId);
                throw;
            }
        }

        private GeocodingResult ParseGeocodingResponse(string jsonResponse, string originalAddress, string requestId)
        {
            _logger.LogDebug("Parsing Azure Maps Geocoding response, requestId={RequestId}", requestId);

            try
            {
                using var doc = JsonDocument.Parse(jsonResponse);
                var root = doc.RootElement;

                if (!root.TryGetProperty("results", out var results) || results.GetArrayLength() == 0)
                {
                    _logger.LogWarning("No geocoding results found for address: \"{Address}\", requestId={RequestId}", originalAddress, requestId);
                    throw new InvalidOperationException("No geocoding results found for the specified address");
                }

                var firstResult = results[0];
                
                // Extract coordinates
                var position = firstResult.GetProperty("position");
                var lat = position.GetProperty("lat").GetDouble();
                var lon = position.GetProperty("lon").GetDouble();

                // Extract address details
                var address = firstResult.GetProperty("address");
                var freeformAddress = address.TryGetProperty("freeformAddress", out var ffa) ? ffa.GetString() ?? originalAddress : originalAddress;
                
                // Extract confidence/score
                var confidence = firstResult.TryGetProperty("score", out var score) ? 
                    MapConfidenceScore(score.GetDouble()) : "Unknown";

                var result = new GeocodingResult
                {
                    Address = originalAddress,
                    Coordinates = new Coordinate { Lat = lat, Lon = lon },
                    FormattedAddress = freeformAddress,
                    Confidence = confidence
                };

                _logger.LogDebug("Geocoding result parsed: lat={Lat}, lon={Lon}, confidence={Confidence}, requestId={RequestId}", 
                    lat, lon, confidence, requestId);

                return result;
            }
            catch (JsonException jsonEx)
            {
                _logger.LogError(jsonEx, "Failed to parse Azure Maps Geocoding JSON response, requestId={RequestId}", requestId);
                throw new InvalidOperationException("Failed to parse Azure Maps Geocoding response", jsonEx);
            }
            catch (KeyNotFoundException keyEx)
            {
                _logger.LogError(keyEx, "Missing expected property in Azure Maps Geocoding response, requestId={RequestId}", requestId);
                throw new InvalidOperationException("Missing expected property in Azure Maps Geocoding response", keyEx);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error parsing Azure Maps Geocoding response, requestId={RequestId}", requestId);
                throw;
            }
        }

        private static string MapConfidenceScore(double score)
        {
            return score switch
            {
                >= 0.9 => "High",
                >= 0.7 => "Medium",
                >= 0.5 => "Low",
                _ => "VeryLow"
            };
        }
    }
}