using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Reflection;
using Xunit;
using Xunit.Abstractions;
using EmergencyManagementMCP.Models;
using EmergencyManagementMCP.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace EmergencyManagementMcp.IntegrationTests
{
    /// <summary>
    /// Test to verify that the RouterClient -> RouteLinkService -> Blob pipeline preserves data correctly
    /// </summary>
    public class BlobDataPipelineTest
    {
        private readonly ITestOutputHelper _output;

        public BlobDataPipelineTest(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void TestRouterClientToRouteLinkServicePipeline()
        {
            _output.WriteLine("=== Router Client to RouteLink Service Pipeline Test ===");
            
            // Test data
            var origin = new Coordinate { Lat = 34.0522, Lon = -118.2437 }; // LA
            var destination = new Coordinate { Lat = 34.1625, Lon = -118.1331 }; // Pasadena
            
            var avoidAreas = new List<AvoidRectangle>
            {
                new AvoidRectangle 
                { 
                    MinLat = 34.0, MinLon = -118.3, 
                    MaxLat = 34.1, MaxLon = -118.2 
                }
            };

            // Create mock configuration and logger for RouterClient
            var config = new ConfigurationBuilder().Build();
            var logger = new LoggerFactory().CreateLogger<RouterClient>();
            
            // Create a mock HttpClient (we won't actually make requests)
            using var httpClient = new HttpClient();
            
            var routerClient = new RouterClient(httpClient, logger, config);

            // Use reflection to test the private method BuildAzureMapsPostRequestJson
            var method = typeof(RouterClient).GetMethod("BuildAzureMapsPostRequestJson", 
                BindingFlags.NonPublic | BindingFlags.Instance);
            
            Assert.NotNull(method);
            
            var azureMapsPostJson = (string)method.Invoke(routerClient, new object[] { origin, destination, avoidAreas })!;
            
            _output.WriteLine("1. RouterClient generated Azure Maps POST JSON:");
            _output.WriteLine(azureMapsPostJson);
            _output.WriteLine("");

            // Verify the generated JSON has the right structure
            var parsed = JsonSerializer.Deserialize<AzureMapsPostData>(azureMapsPostJson);
            Assert.NotNull(parsed);
            Assert.Equal("FeatureCollection", parsed.Type);
            Assert.Equal("driving", parsed.TravelMode);
            Assert.Equal(2, parsed.Features.Length); // Should have 2 features
            Assert.NotNull(parsed.AvoidAreas); // Should have avoid areas
            Assert.Equal("MultiPolygon", parsed.AvoidAreas.Type);
            
            _output.WriteLine("✅ RouterClient generates correct data structure:");
            _output.WriteLine($"  - Features count: {parsed.Features.Length}");
            _output.WriteLine($"  - Avoid areas: {(parsed.AvoidAreas == null ? "null" : "present")}");

            // Now let's simulate what RouteLinkService.CreateAsync would do with this JSON
            // This mimics the logic in lines 210-233 of RouteLinkService.cs
            var postData = JsonSerializer.Deserialize<JsonElement>(azureMapsPostJson);
            
            var storageData = new Dictionary<string, object?>
            {
                ["type"] = postData.GetProperty("type").GetString(),
                ["features"] = JsonSerializer.Deserialize<object>(postData.GetProperty("features").GetRawText()),
                ["travelMode"] = postData.GetProperty("travelMode").GetString(),
                ["routeOutputOptions"] = JsonSerializer.Deserialize<string[]>(postData.GetProperty("routeOutputOptions").GetRawText()),
                ["ttlMinutes"] = 1440 // Example TTL
            };
            
            // Add avoidAreas if it exists
            if (postData.TryGetProperty("avoidAreas", out var avoidAreasElement) && avoidAreasElement.ValueKind != JsonValueKind.Null)
            {
                storageData["avoidAreas"] = JsonSerializer.Deserialize<object>(avoidAreasElement.GetRawText());
                _output.WriteLine("✅ RouteLinkService would preserve avoid areas");
            }
            else
            {
                _output.WriteLine("❌ RouteLinkService would NOT preserve avoid areas");
            }
            
            var storageJson = JsonSerializer.Serialize(storageData, new JsonSerializerOptions 
            { 
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            });
            
            _output.WriteLine("");
            _output.WriteLine("2. What RouteLinkService would store in blob:");
            _output.WriteLine(storageJson);

            // Now test what the RouteLinksFunction public endpoint would return
            // This mimics the logic in lines 340-355 of RouteLinksFunction.cs
            var routeSpec = JsonSerializer.Deserialize<RouteSpec>(storageJson);
            Assert.NotNull(routeSpec);
            
            var azureMapsPostData = new AzureMapsPostData
            {
                Type = routeSpec.Type,
                Features = routeSpec.Features,
                AvoidAreas = routeSpec.AvoidAreas,
                RouteOutputOptions = routeSpec.RouteOutputOptions,
                TravelMode = routeSpec.TravelMode
            };

            var finalJson = JsonSerializer.Serialize(azureMapsPostData, new JsonSerializerOptions 
            { 
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            });
            
            _output.WriteLine("");
            _output.WriteLine("3. What MapPage.tsx would receive from public endpoint:");
            _output.WriteLine(finalJson);

            // Final assertions
            Assert.Equal("FeatureCollection", azureMapsPostData.Type);
            Assert.Equal("driving", azureMapsPostData.TravelMode);
            Assert.Equal(2, azureMapsPostData.Features.Length); // Should still have 2 features
            Assert.NotNull(azureMapsPostData.AvoidAreas); // Should still have avoid areas
            Assert.Equal("MultiPolygon", azureMapsPostData.AvoidAreas.Type);
            
            _output.WriteLine("");
            _output.WriteLine("✅ Full pipeline test passed:");
            _output.WriteLine($"  - Final features count: {azureMapsPostData.Features.Length}");
            _output.WriteLine($"  - Final avoid areas: {(azureMapsPostData.AvoidAreas == null ? "null" : "present")}");
            _output.WriteLine($"  - Pipeline preserves data correctly");
        }
    }
}