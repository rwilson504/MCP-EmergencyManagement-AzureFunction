using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Linq;
using Xunit;
using Xunit.Abstractions;
using EmergencyManagementMCP.Models;

namespace EmergencyManagementMcp.IntegrationTests
{
    /// <summary>
    /// Test the first CreateAsync method in RouteLinkService to see if it creates problematic data
    /// </summary>
    public class FirstCreateAsyncMethodTest
    {
        private readonly ITestOutputHelper _output;

        public FirstCreateAsyncMethodTest(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void TestFirstCreateAsyncMethodOutput()
        {
            _output.WriteLine("=== Testing First CreateAsync Method Logic ===");
            
            // Simulate what the first CreateAsync method does (lines 80-119 in RouteLinkService.cs)
            var origin = new Coordinate { Lat = 34.0522, Lon = -118.2437 };
            var destination = new Coordinate { Lat = 34.1625, Lon = -118.1331 };
            
            // This method receives appliedAvoids as string[] (like "minLon,minLat,maxLon,maxLat")
            var appliedAvoids = new[] { "-118.3,34.0,-118.2,34.1" }; // String avoid areas
            
            var ttl = TimeSpan.FromMinutes(1440);

            // Create RouteSpec as the first method does
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
                TtlMinutes = (int)ttl.TotalMinutes
                // Note: AvoidAreas is NOT populated - this is the bug!
            };
            
            var json = JsonSerializer.Serialize(routeSpec);
            
            _output.WriteLine("First method would create this RouteSpec JSON:");
            _output.WriteLine(json);
            
            // Test what happens when RouteLinksFunction retrieves this
            var retrievedRouteSpec = JsonSerializer.Deserialize<RouteSpec>(json);
            Assert.NotNull(retrievedRouteSpec);
            
            // Convert to AzureMapsPostData as RouteLinksFunction does (lines 347-355)
            var azureMapsPostData = new AzureMapsPostData
            {
                Type = retrievedRouteSpec.Type,
                Features = retrievedRouteSpec.Features,
                AvoidAreas = retrievedRouteSpec.AvoidAreas, // This will be NULL!
                RouteOutputOptions = retrievedRouteSpec.RouteOutputOptions,
                TravelMode = retrievedRouteSpec.TravelMode
            };

            var finalJson = JsonSerializer.Serialize(azureMapsPostData, new JsonSerializerOptions 
            { 
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            });
            
            _output.WriteLine("");
            _output.WriteLine("What MapPage.tsx would receive from first method blob:");
            _output.WriteLine(finalJson);

            // Verify this has the correct features but missing avoidAreas
            Assert.Equal(2, azureMapsPostData.Features.Length); // Should have features
            Assert.Null(azureMapsPostData.AvoidAreas); // This is the problem - should have avoid areas but doesn't
            
            _output.WriteLine("");
            _output.WriteLine("✅ Analysis complete:");
            _output.WriteLine($"  - Features count: {azureMapsPostData.Features.Length} (correct)");
            _output.WriteLine($"  - Avoid areas: {(azureMapsPostData.AvoidAreas == null ? "null" : "present")} (MISSING!)");
            _output.WriteLine("  - This explains why MapPage.tsx gets null avoidAreas");
            _output.WriteLine("  - But doesn't explain empty features array from the issue");
        }

        [Fact] 
        public void TestWhenDefaultRouteSpecIsUsed()
        {
            _output.WriteLine("=== Testing When Default RouteSpec Is Used ===");
            
            // Test what happens if somehow a default RouteSpec gets stored
            var defaultRouteSpec = new RouteSpec(); // Default constructor
            var json = JsonSerializer.Serialize(defaultRouteSpec);
            
            _output.WriteLine("Default RouteSpec JSON:");
            _output.WriteLine(json);
            
            // Convert to AzureMapsPostData as RouteLinksFunction would
            var azureMapsPostData = new AzureMapsPostData
            {
                Type = defaultRouteSpec.Type,
                Features = defaultRouteSpec.Features,
                AvoidAreas = defaultRouteSpec.AvoidAreas,
                RouteOutputOptions = defaultRouteSpec.RouteOutputOptions,
                TravelMode = defaultRouteSpec.TravelMode
            };

            var finalJson = JsonSerializer.Serialize(azureMapsPostData, new JsonSerializerOptions 
            { 
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            });
            
            _output.WriteLine("");
            _output.WriteLine("What MapPage.tsx would receive from default RouteSpec:");
            _output.WriteLine(finalJson);

            // This EXACTLY matches the issue description!
            Assert.Equal("FeatureCollection", azureMapsPostData.Type);
            Assert.Equal("driving", azureMapsPostData.TravelMode);
            Assert.Empty(azureMapsPostData.Features); // Empty features array - matches issue!
            Assert.Null(azureMapsPostData.AvoidAreas); // Null avoid areas - matches issue!
            Assert.Contains("routePath", azureMapsPostData.RouteOutputOptions); // Matches issue!
            
            _output.WriteLine("");
            _output.WriteLine("🎯 FOUND THE EXACT MATCH:");
            _output.WriteLine("  - Default RouteSpec produces EXACTLY the problematic JSON from the issue");
            _output.WriteLine("  - Empty features[] array");
            _output.WriteLine("  - null avoidAreas");  
            _output.WriteLine("  - routeOutputOptions: ['routePath']");
            _output.WriteLine("  - travelMode: 'driving'");
        }
    }
}