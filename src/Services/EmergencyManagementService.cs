using System.Text.Json;
using System.Net.Http.Json;
using EmergencyManagementMCP.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace EmergencyManagementMCP.Services
{
    public class EmergencyManagementService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<EmergencyManagementService> _logger;
        private readonly string _apiKey;
        private readonly string _apiHost;
        public EmergencyManagementService(HttpClient httpClient, ILogger<EmergencyManagementService> logger, IConfiguration config)
        {
            _logger = logger;
            _logger.LogInformation("EmergencyManagementService initialized");
            _httpClient = httpClient;
            _apiKey = config["VA_API_KEY"] ?? string.Empty;
            _apiHost = config["VA_API_HOST"] ?? "sandbox-api.va.gov";
        }

        private void AddApiKeyHeader(HttpRequestMessage request)
        {
            if (!string.IsNullOrEmpty(_apiKey))
            {
                request.Headers.Add("apikey", _apiKey);
            }
        }
        
        // List Facilities
        public async Task<object> ListFacilitiesAsync(Dictionary<string, object> parameters)
        {
            var query = BuildQueryString(parameters);
            var request = new HttpRequestMessage(HttpMethod.Get, $"https://{_apiHost}/services/va_facilities/v1/facilities{query}");
            AddApiKeyHeader(request);
            var response = await _httpClient.SendAsync(request);
            var result = await HandleResponseAsync(response);

            // Check if result is empty (assume result is a JSON object with a 'data' array)
            bool isEmpty = false;
            try
            {
                var json = JsonSerializer.Serialize(result);
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("data", out var dataElem) && dataElem.ValueKind == JsonValueKind.Array && dataElem.GetArrayLength() == 0)
                {
                    isEmpty = true;
                }
            }
            catch { }

            // If empty and radius is present, expand radius and retry
            if (isEmpty && parameters.ContainsKey("radius") && parameters["radius"] is double radius && radius < 100)
            {
                double expandedRadius = radius * 2;
                parameters["radius"] = expandedRadius;
                var expandedQuery = BuildQueryString(parameters);
                var expandedRequest = new HttpRequestMessage(HttpMethod.Get, $"https://{_apiHost}/services/va_facilities/v1/facilities{expandedQuery}");
                AddApiKeyHeader(expandedRequest);
                var expandedResponse = await _httpClient.SendAsync(expandedRequest);
                var expandedResult = await HandleResponseAsync(expandedResponse);
                return new {
                    message = $"No facilities found within {radius} miles. Expanded search to {expandedRadius} miles.",
                    data = expandedResult
                };
            }

            return result;
        }

        // Get Facility By ID
        public async Task<object> GetFacilityByIdAsync(string facilityId)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"https://{_apiHost}/services/va_facilities/v1/facilities/{facilityId}");
            AddApiKeyHeader(request);
            var response = await _httpClient.SendAsync(request);
            return await HandleResponseAsync(response);
        }

        // List Facility Services
        public async Task<object> ListFacilityServicesAsync(string facilityId, string serviceIds = null, string serviceType = null)
        {
            var query = BuildQueryString(new Dictionary<string, object>
            {
                { "serviceIds", serviceIds },
                { "serviceType", serviceType }
            });
            var request = new HttpRequestMessage(HttpMethod.Get, $"https://{_apiHost}/services/va_facilities/v1/facilities/{facilityId}/services{query}");
            AddApiKeyHeader(request);
            var response = await _httpClient.SendAsync(request);
            return await HandleResponseAsync(response);
        }

        // Get Facility Service Details
        public async Task<object> GetFacilityServiceDetailsAsync(string facilityId, string serviceId)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"https://{_apiHost}/services/va_facilities/v1/facilities/{facilityId}/services/{serviceId}");
            AddApiKeyHeader(request);
            var response = await _httpClient.SendAsync(request);
            return await HandleResponseAsync(response);
        }

        // List Facility IDs
        public async Task<object> ListFacilityIdsAsync(string type = null)
        {
            var query = BuildQueryString(new Dictionary<string, object>
            {
                { "type", type }
            });
            var request = new HttpRequestMessage(HttpMethod.Get, $"https://{_apiHost}/services/va_facilities/v1/ids{query}");
            AddApiKeyHeader(request);
            var response = await _httpClient.SendAsync(request);
            return await HandleResponseAsync(response);
        }

        // List Nearby Facilities
        public async Task<object> ListNearbyFacilitiesAsync(double lat, double lon, int? driveTime = null, string services = null, int? page = null, int? perPage = null)
        {
            var parameters = new Dictionary<string, object>
            {
                { "lat", lat },
                { "long", lon },
                { "drive_time", driveTime },
                { "services", services },
                { "page", page },
                { "per_page", perPage }
            };
            var query = BuildQueryString(parameters);
            var request = new HttpRequestMessage(HttpMethod.Get, $"https://{_apiHost}/services/va_facilities/v1/nearby{query}");
            AddApiKeyHeader(request);
            var response = await _httpClient.SendAsync(request);
            return await HandleResponseAsync(response);
        }

        // Helper: Build query string from parameters
        private string BuildQueryString(Dictionary<string, object> parameters)
        {
            var query = new List<string>();
            foreach (var kvp in parameters)
            {
                if (kvp.Value != null)
                {
                    query.Add($"{kvp.Key}={Uri.EscapeDataString(kvp.Value.ToString())}");
                }
            }
            return query.Count > 0 ? "?" + string.Join("&", query) : string.Empty;
        }

        // Helper: Handle HTTP response
        private async Task<object> HandleResponseAsync(HttpResponseMessage response)
        {
            var content = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("VA API call failed. Status Code: {StatusCode}, Error: {ErrorContent}", response.StatusCode, content);
                return new ErrorResponse($"VA API call failed: {content}", (int)response.StatusCode);
            }
            try
            {
                return JsonSerializer.Deserialize<object>(content);
            }
            catch
            {
                return content;
            }
        }
    }
}