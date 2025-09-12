using System;
using System.Collections.Generic;
using System.Text.Json;
using Xunit;
using Xunit.Abstractions;
using EmergencyManagementMCP.Models;
using EmergencyManagementMCP.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace EmergencyManagementMcp.IntegrationTests
{
    /// <summary>
    /// Test to validate that RouterClient now generates the exact JSON POST body 
    /// that MapPage.tsx would send to Azure Maps API.
    /// </summary>
    public class RouterClientPostJsonTest
    {
        private readonly ITestOutputHelper _output;

        public RouterClientPostJsonTest(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void RouterClient_Should_Generate_MapPage_Compatible_JSON()
        {
            _output.WriteLine("RouterClient POST JSON Generation Test");
            _output.WriteLine("=====================================");
            
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

            // Create mock configuration and logger
            var config = new ConfigurationBuilder().Build();
            var logger = new LoggerFactory().CreateLogger<RouterClient>();
            
            // Create a mock HttpClient (we won't actually make requests in this test)
            using var httpClient = new HttpClient();
            
            var routerClient = new RouterClient(httpClient, logger, config);

            // Use reflection to test the private method (for testing purposes)
            var method = typeof(RouterClient).GetMethod("BuildAzureMapsPostRequestJson", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            Assert.NotNull(method);
            
            var postJson = (string)method.Invoke(routerClient, new object[] { origin, destination, avoidAreas });
            
            _output.WriteLine("Generated POST JSON:");
            _output.WriteLine(postJson);
            _output.WriteLine("");

            // Verify the JSON structure
            var jsonDoc = JsonDocument.Parse(postJson);
            var root = jsonDoc.RootElement;

            // Validate basic structure
            Assert.Equal("FeatureCollection", root.GetProperty("type").GetString());
            Assert.Equal("driving", root.GetProperty("travelMode").GetString());
            Assert.Equal(2, root.GetProperty("features").GetArrayLength()); // origin + destination
            Assert.True(root.GetProperty("routeOutputOptions").GetArrayLength() >= 1);
            
            // Validate features structure
            var features = root.GetProperty("features");
            var originFeature = features[0];
            var destFeature = features[1];
            
            // Check origin feature
            Assert.Equal("Feature", originFeature.GetProperty("type").GetString());
            Assert.Equal("Point", originFeature.GetProperty("geometry").GetProperty("type").GetString());
            Assert.Equal(0, originFeature.GetProperty("properties").GetProperty("pointIndex").GetInt32());
            Assert.Equal("waypoint", originFeature.GetProperty("properties").GetProperty("pointType").GetString());
            
            // Check coordinates (should be [lon, lat] for GeoJSON)
            var originCoords = originFeature.GetProperty("geometry").GetProperty("coordinates");
            Assert.Equal(origin.Lon, originCoords[0].GetDouble());
            Assert.Equal(origin.Lat, originCoords[1].GetDouble());
            
            // Check destination feature
            Assert.Equal("Feature", destFeature.GetProperty("type").GetString());
            Assert.Equal("Point", destFeature.GetProperty("geometry").GetProperty("type").GetString());
            Assert.Equal(1, destFeature.GetProperty("properties").GetProperty("pointIndex").GetInt32());
            Assert.Equal("waypoint", destFeature.GetProperty("properties").GetProperty("pointType").GetString());
            
            var destCoords = destFeature.GetProperty("geometry").GetProperty("coordinates");
            Assert.Equal(destination.Lon, destCoords[0].GetDouble());
            Assert.Equal(destination.Lat, destCoords[1].GetDouble());

            // Check avoid areas
            Assert.True(root.TryGetProperty("avoidAreas", out var avoidAreasElement));
            Assert.Equal("MultiPolygon", avoidAreasElement.GetProperty("type").GetString());
            Assert.Equal(1, avoidAreasElement.GetProperty("coordinates").GetArrayLength());

            _output.WriteLine("✅ POST JSON structure validation passed:");
            _output.WriteLine($"  - Type: {root.GetProperty("type").GetString()}");
            _output.WriteLine($"  - Travel mode: {root.GetProperty("travelMode").GetString()}");
            _output.WriteLine($"  - Features: {root.GetProperty("features").GetArrayLength()}");
            _output.WriteLine($"  - Avoid areas: {(avoidAreasElement.ValueKind != JsonValueKind.Null ? "Yes" : "No")}");
            _output.WriteLine($"  - JSON length: {postJson.Length} characters");
            
            // Verify this matches the expected MapPage.tsx format
            Assert.Contains("\"type\":\"FeatureCollection\"", postJson);
            Assert.Contains("\"travelMode\":\"driving\"", postJson);
            Assert.Contains("\"pointType\":\"waypoint\"", postJson);
            Assert.Contains("\"avoidAreas\":", postJson);
            Assert.DoesNotContain("ttlMinutes", postJson); // Should not be in the POST body

            _output.WriteLine("✅ JSON format matches MapPage.tsx expectations");
        }
    }
}