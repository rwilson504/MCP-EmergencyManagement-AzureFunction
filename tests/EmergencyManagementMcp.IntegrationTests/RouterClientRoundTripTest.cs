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
    /// Test the exact round-trip issue in RouterClient.GetRouteWithRequestDataAsync
    /// </summary>
    public class RouterClientRoundTripTest
    {
        private readonly ITestOutputHelper _output;

        public RouterClientRoundTripTest(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void TestRouterClientRoundTrip()
        {
            _output.WriteLine("=== Testing RouterClient Round-Trip Issue ===");
            
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

            // Create RouterClient
            var config = new ConfigurationBuilder().Build();
            var logger = new LoggerFactory().CreateLogger<RouterClient>();
            using var httpClient = new HttpClient();
            var routerClient = new RouterClient(httpClient, logger, config);

            // Use reflection to call BuildAzureMapsPostRequestJson
            var method = typeof(RouterClient).GetMethod("BuildAzureMapsPostRequestJson", 
                BindingFlags.NonPublic | BindingFlags.Instance);
            
            Assert.NotNull(method);
            
            var postRequestJson = (string)method.Invoke(routerClient, new object[] { origin, destination, avoidAreas })!;
            
            _output.WriteLine("1. Generated POST JSON:");
            _output.WriteLine(postRequestJson);
            _output.WriteLine($"   JSON Length: {postRequestJson.Length}");

            // Now test the round-trip deserialization (this is what fails in line 301)
            AzureMapsPostData? deserializedData = null;
            try 
            {
                deserializedData = JsonSerializer.Deserialize<AzureMapsPostData>(postRequestJson);
                _output.WriteLine($"2. Deserialization result: {(deserializedData == null ? "null" : "success")}");
                
                if (deserializedData != null)
                {
                    _output.WriteLine($"   Features count: {deserializedData.Features.Length}");
                    _output.WriteLine($"   Avoid areas: {(deserializedData.AvoidAreas == null ? "null" : "present")}");
                    _output.WriteLine($"   Travel mode: {deserializedData.TravelMode}");
                }
            }
            catch (Exception ex)
            {
                _output.WriteLine($"2. Deserialization FAILED: {ex.GetType().Name}: {ex.Message}");
            }

            // Test what would happen with the fallback
            var fallbackData = deserializedData ?? new AzureMapsPostData();
            var fallbackJson = JsonSerializer.Serialize(fallbackData, new JsonSerializerOptions 
            { 
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            });
            
            _output.WriteLine("");
            _output.WriteLine("3. Final result JSON (what would go to RouteLinkService):");
            _output.WriteLine(fallbackJson);

            // Check if this matches the issue
            Assert.NotNull(fallbackData);
            if (deserializedData == null)
            {
                _output.WriteLine("");
                _output.WriteLine("❌ FOUND THE BUG: Deserialization returned null, fallback created empty structure!");
                Assert.Empty(fallbackData.Features); // This would be the empty array from issue
                Assert.Null(fallbackData.AvoidAreas); // This would be the null from issue  
            }
            else
            {
                _output.WriteLine("");
                _output.WriteLine("✅ Round-trip successful, no empty structure created");
                Assert.NotEmpty(fallbackData.Features);
            }
        }

        [Fact]
        public void TestWithEmptyAvoidAreas()
        {
            _output.WriteLine("=== Testing With No Avoid Areas ===");
            
            // Test with empty avoid areas (no fire perimeters found)
            var origin = new Coordinate { Lat = 34.0522, Lon = -118.2437 };
            var destination = new Coordinate { Lat = 34.1625, Lon = -118.1331 };
            var emptyAvoidAreas = new List<AvoidRectangle>(); // Empty list

            var config = new ConfigurationBuilder().Build();
            var logger = new LoggerFactory().CreateLogger<RouterClient>();
            using var httpClient = new HttpClient();
            var routerClient = new RouterClient(httpClient, logger, config);

            var method = typeof(RouterClient).GetMethod("BuildAzureMapsPostRequestJson", 
                BindingFlags.NonPublic | BindingFlags.Instance);
            
            Assert.NotNull(method);
            
            var postRequestJson = (string)method.Invoke(routerClient, new object[] { origin, destination, emptyAvoidAreas })!;
            
            _output.WriteLine("Generated POST JSON with empty avoid areas:");
            _output.WriteLine(postRequestJson);

            var deserializedData = JsonSerializer.Deserialize<AzureMapsPostData>(postRequestJson);
            
            Assert.NotNull(deserializedData);
            Assert.Equal(2, deserializedData.Features.Length); // Should still have 2 waypoints
            Assert.Null(deserializedData.AvoidAreas); // Should be null (not empty array) 
            Assert.Equal("driving", deserializedData.TravelMode);
            
            _output.WriteLine("");
            _output.WriteLine("✅ Empty avoid areas case works correctly:");
            _output.WriteLine($"  - Features: {deserializedData.Features.Length} (should be 2)");
            _output.WriteLine($"  - Avoid areas: {(deserializedData.AvoidAreas == null ? "null" : "present")} (should be null)");
        }
    }
}