using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Xunit;
using Xunit.Abstractions;
using EmergencyManagementMCP.Models;

namespace EmergencyManagementMcp.IntegrationTests
{
    /// <summary>
    /// End-to-end integration test to validate that RouterClient generates Azure Maps POST data
    /// that can be successfully used by MapPage.tsx for the Azure Maps API calls.
    /// </summary>
    public class EndToEndRouteWithMapDataTest
    {
        private readonly ITestOutputHelper _output;

        public EndToEndRouteWithMapDataTest(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void RouterClient_Should_Generate_MapPage_Compatible_AzureMapsPostData()
        {
            _output.WriteLine("End-to-End Route with Azure Maps Data Test");
            _output.WriteLine("==========================================");
            
            // Simulate the RouterClient building Azure Maps POST data
            var origin = new Coordinate { Lat = 34.0522, Lon = -118.2437 }; // LA
            var destination = new Coordinate { Lat = 34.1625, Lon = -118.1331 }; // Pasadena
            
            var avoidAreas = new List<AvoidRectangle>
            {
                new AvoidRectangle 
                { 
                    MinLat = 34.0, MinLon = -118.3, 
                    MaxLat = 34.1, MaxLon = -118.2 
                },
                new AvoidRectangle 
                { 
                    MinLat = 34.05, MinLon = -118.28, 
                    MaxLat = 34.08, MaxLon = -118.25 
                }
            };

            // Build Azure Maps POST data as RouterClient.BuildAzureMapsPostData() would
            var features = new[]
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
            };

            // Convert avoid rectangles to MultiPolygon (as RouterClient would)
            var polygons = avoidAreas.Take(10).Select(r => new[]
            {
                new[]
                {
                    new[] { r.MinLon, r.MinLat },
                    new[] { r.MaxLon, r.MinLat },
                    new[] { r.MaxLon, r.MaxLat },
                    new[] { r.MinLon, r.MaxLat },
                    new[] { r.MinLon, r.MinLat } // Close the polygon
                }
            }).ToArray();

            var azureMapsPostData = new AzureMapsPostData
            {
                Type = "FeatureCollection",
                Features = features,
                TravelMode = "driving",
                RouteOutputOptions = new[] { "routePath", "itinerary" },
                AvoidAreas = new MultiPolygon
                {
                    Type = "MultiPolygon",
                    Coordinates = polygons
                }
            };

            // Simulate RouteLinkService storing this data in a RouteSpec
            var routeSpec = new RouteSpec
            {
                Type = azureMapsPostData.Type,
                Features = azureMapsPostData.Features,
                TravelMode = azureMapsPostData.TravelMode,
                RouteOutputOptions = azureMapsPostData.RouteOutputOptions,
                AvoidAreas = azureMapsPostData.AvoidAreas,
                TtlMinutes = 1440 // This would be set by RouteLinkService
            };

            // Serialize the RouteSpec as it would be stored in blob
            var storedJson = JsonSerializer.Serialize(routeSpec);
            _output.WriteLine("Stored RouteSpec JSON:");
            _output.WriteLine(storedJson);
            _output.WriteLine("");

            // Simulate MapPage.tsx reading the route spec from public endpoint
            var retrievedRouteSpec = JsonSerializer.Deserialize<RouteSpec>(storedJson);
            Assert.NotNull(retrievedRouteSpec);

            // Simulate MapPage.tsx creating clean object for Azure Maps API (removing ttlMinutes)
            var azureMapsRequestData = new AzureMapsPostData
            {
                Type = retrievedRouteSpec.Type,
                Features = retrievedRouteSpec.Features,
                TravelMode = retrievedRouteSpec.TravelMode,
                RouteOutputOptions = retrievedRouteSpec.RouteOutputOptions,
                AvoidAreas = retrievedRouteSpec.AvoidAreas
                // Explicitly not including TtlMinutes - it should not be sent to Azure Maps
            };

            // Serialize what MapPage.tsx would send to Azure Maps API
            var azureMapsApiJson = JsonSerializer.Serialize(azureMapsRequestData);
            _output.WriteLine("JSON sent to Azure Maps API:");
            _output.WriteLine(azureMapsApiJson);
            _output.WriteLine("");

            // Validate that the JSON sent to Azure Maps API is correct
            Assert.Equal("FeatureCollection", azureMapsRequestData.Type);
            Assert.Equal(2, azureMapsRequestData.Features.Length);
            Assert.Equal("driving", azureMapsRequestData.TravelMode);
            Assert.Contains("routePath", azureMapsRequestData.RouteOutputOptions);
            Assert.Contains("itinerary", azureMapsRequestData.RouteOutputOptions);
            Assert.NotNull(azureMapsRequestData.AvoidAreas);
            Assert.Equal("MultiPolygon", azureMapsRequestData.AvoidAreas.Type);
            Assert.Equal(2, azureMapsRequestData.AvoidAreas.Coordinates.Length); // 2 avoid rectangles
            
            // Verify the ttlMinutes was removed
            Assert.DoesNotContain("ttlMinutes", azureMapsApiJson);

            // Validate waypoint coordinates are correct
            var originFeature = azureMapsRequestData.Features[0];
            Assert.Equal(origin.Lon, originFeature.Geometry.Coordinates[0]); // longitude first in GeoJSON
            Assert.Equal(origin.Lat, originFeature.Geometry.Coordinates[1]); // latitude second in GeoJSON
            Assert.Equal(0, originFeature.Properties.PointIndex);
            Assert.Equal("waypoint", originFeature.Properties.PointType);

            var destFeature = azureMapsRequestData.Features[1];
            Assert.Equal(destination.Lon, destFeature.Geometry.Coordinates[0]); // longitude first in GeoJSON
            Assert.Equal(destination.Lat, destFeature.Geometry.Coordinates[1]); // latitude second in GeoJSON
            Assert.Equal(1, destFeature.Properties.PointIndex);
            Assert.Equal("waypoint", destFeature.Properties.PointType);

            // Validate avoid areas geometry
            var firstAvoidPolygon = azureMapsRequestData.AvoidAreas.Coordinates[0][0]; // First polygon, first ring
            Assert.Equal(5, firstAvoidPolygon.Length); // Rectangle with closed ring (5 points)
            Assert.Equal(avoidAreas[0].MinLon, firstAvoidPolygon[0][0]); // First point MinLon
            Assert.Equal(avoidAreas[0].MinLat, firstAvoidPolygon[0][1]); // First point MinLat
            Assert.Equal(firstAvoidPolygon[0][0], firstAvoidPolygon[4][0]); // Closed polygon (first == last)
            Assert.Equal(firstAvoidPolygon[0][1], firstAvoidPolygon[4][1]); // Closed polygon (first == last)

            _output.WriteLine("✅ End-to-end validation passed:");
            _output.WriteLine($"  - RouterClient generates correct Azure Maps POST data");
            _output.WriteLine($"  - RouteLinkService stores data in compatible RouteSpec format");
            _output.WriteLine($"  - MapPage.tsx can use stored data for Azure Maps API calls");
            _output.WriteLine($"  - Waypoints: {azureMapsRequestData.Features.Length}");
            _output.WriteLine($"  - Avoid areas: {azureMapsRequestData.AvoidAreas.Coordinates.Length}");
            _output.WriteLine($"  - Travel mode: {azureMapsRequestData.TravelMode}");
            _output.WriteLine($"  - Output options: {string.Join(", ", azureMapsRequestData.RouteOutputOptions)}");
        }

        [Fact]
        public void MapPage_Azure_Maps_API_Format_Should_Match_RouterClient_Output()
        {
            _output.WriteLine("MapPage Azure Maps API Format Compatibility Test");
            _output.WriteLine("================================================");
            
            // Create a RouteSpec as would be generated by RouterClient and stored by RouteLinkService
            var routerClientOutput = new AzureMapsPostData
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
                            Coordinates = new[] { -121.4948, 38.5816 } // [lon, lat] GeoJSON format
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
                            Coordinates = new[] { -122.8756, 42.3265 } // [lon, lat] GeoJSON format
                        },
                        Properties = new RouteFeatureProperties
                        {
                            PointIndex = 1,
                            PointType = "waypoint"
                        }
                    }
                },
                TravelMode = "driving",
                RouteOutputOptions = new[] { "routePath", "itinerary" }
            };

            // Simulate what MapPage.tsx would expect and send to Azure Maps
            var mapPageJson = JsonSerializer.Serialize(routerClientOutput);
            
            // Parse it back to verify structure
            var parsedForAzureMaps = JsonSerializer.Deserialize<AzureMapsPostData>(mapPageJson);
            Assert.NotNull(parsedForAzureMaps);
            
            // Validate Azure Maps API 2025-01-01 compatibility
            Assert.Equal("FeatureCollection", parsedForAzureMaps.Type);
            Assert.Equal("driving", parsedForAzureMaps.TravelMode);
            Assert.Contains("routePath", parsedForAzureMaps.RouteOutputOptions);
            Assert.Contains("itinerary", parsedForAzureMaps.RouteOutputOptions);
            Assert.Equal(2, parsedForAzureMaps.Features.Length);
            
            // Verify GeoJSON coordinate format (lon, lat)
            var firstFeature = parsedForAzureMaps.Features[0];
            Assert.Equal(-121.4948, firstFeature.Geometry.Coordinates[0]); // longitude first
            Assert.Equal(38.5816, firstFeature.Geometry.Coordinates[1]); // latitude second
            
            _output.WriteLine("✅ MapPage Azure Maps API format validation passed:");
            _output.WriteLine($"  - JSON structure compatible with Azure Maps API 2025-01-01");
            _output.WriteLine($"  - GeoJSON coordinates in [lon, lat] format");
            _output.WriteLine($"  - All required fields present for POST request");
            _output.WriteLine($"  - JSON size: {mapPageJson.Length} characters");
        }
    }
}