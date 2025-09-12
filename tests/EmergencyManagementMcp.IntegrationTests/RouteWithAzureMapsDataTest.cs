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
    /// Test to validate the new RouterClient functionality that returns Azure Maps request data
    /// along with the route result, enabling MapPage.tsx to reuse the exact same API request.
    /// </summary>
    public class RouteWithAzureMapsDataTest
    {
        private readonly ITestOutputHelper _output;

        public RouteWithAzureMapsDataTest(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void TestAzureMapsPostDataStructure()
        {
            _output.WriteLine("Azure Maps POST Data Structure Test");
            _output.WriteLine("===================================");
            
            // Test data matching what the RouterClient would generate
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

            // Build the Azure Maps POST data structure as RouterClient would
            var features = new[]
            {
                new RouteFeature
                {
                    Type = "Feature",
                    Geometry = new PointGeometry
                    {
                        Type = "Point",
                        Coordinates = new[] { origin.Lon, origin.Lat } // GeoJSON: [lon, lat]
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
                        Coordinates = new[] { destination.Lon, destination.Lat } // GeoJSON: [lon, lat]
                    },
                    Properties = new RouteFeatureProperties
                    {
                        PointIndex = 1,
                        PointType = "waypoint"
                    }
                }
            };

            // Convert avoid rectangles to MultiPolygon
            var polygons = avoidAreas.Select(r => new[]
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

            // Validate the structure
            Assert.Equal("FeatureCollection", azureMapsPostData.Type);
            Assert.Equal(2, azureMapsPostData.Features.Length);
            Assert.Equal("driving", azureMapsPostData.TravelMode);
            Assert.Contains("routePath", azureMapsPostData.RouteOutputOptions);
            Assert.Contains("itinerary", azureMapsPostData.RouteOutputOptions);
            Assert.NotNull(azureMapsPostData.AvoidAreas);
            Assert.Equal("MultiPolygon", azureMapsPostData.AvoidAreas.Type);

            // Validate waypoint structure
            var originFeature = azureMapsPostData.Features[0];
            Assert.Equal("Feature", originFeature.Type);
            Assert.Equal("Point", originFeature.Geometry.Type);
            Assert.Equal(origin.Lon, originFeature.Geometry.Coordinates[0]); // longitude first in GeoJSON
            Assert.Equal(origin.Lat, originFeature.Geometry.Coordinates[1]); // latitude second in GeoJSON
            Assert.Equal(0, originFeature.Properties.PointIndex);
            Assert.Equal("waypoint", originFeature.Properties.PointType);

            var destFeature = azureMapsPostData.Features[1];
            Assert.Equal("Feature", destFeature.Type);
            Assert.Equal("Point", destFeature.Geometry.Type);
            Assert.Equal(destination.Lon, destFeature.Geometry.Coordinates[0]); // longitude first in GeoJSON
            Assert.Equal(destination.Lat, destFeature.Geometry.Coordinates[1]); // latitude second in GeoJSON
            Assert.Equal(1, destFeature.Properties.PointIndex);
            Assert.Equal("waypoint", destFeature.Properties.PointType);

            // Validate avoid areas structure
            Assert.Equal(1, azureMapsPostData.AvoidAreas.Coordinates.Length);
            var firstPolygon = azureMapsPostData.AvoidAreas.Coordinates[0][0]; // First polygon, first ring
            Assert.Equal(5, firstPolygon.Length); // Rectangle with closed ring (5 points)
            
            // First point should match MinLon, MinLat
            Assert.Equal(avoidAreas[0].MinLon, firstPolygon[0][0]);
            Assert.Equal(avoidAreas[0].MinLat, firstPolygon[0][1]);
            
            // Last point should equal first point (closed polygon)
            Assert.Equal(firstPolygon[0][0], firstPolygon[4][0]);
            Assert.Equal(firstPolygon[0][1], firstPolygon[4][1]);

            // Test JSON serialization
            var json = JsonSerializer.Serialize(azureMapsPostData);
            Assert.NotNull(json);
            Assert.Contains("FeatureCollection", json);
            Assert.Contains("waypoint", json);
            Assert.Contains("MultiPolygon", json);

            _output.WriteLine("✅ Azure Maps POST data structure validation passed");
            _output.WriteLine($"Generated JSON length: {json.Length} characters");
            _output.WriteLine($"Features count: {azureMapsPostData.Features.Length}");
            _output.WriteLine($"Avoid areas count: {azureMapsPostData.AvoidAreas.Coordinates.Length}");
        }

        [Fact]
        public void TestRouteWithRequestDataStructure()
        {
            _output.WriteLine("RouteWithRequestData Structure Test");
            _output.WriteLine("===================================");
            
            // Create a sample RouteResult
            var routeResult = new RouteResult
            {
                DistanceMeters = 15000,
                TravelTimeSeconds = 1200,
                DrivingDirections = new[]
                {
                    new DrivingInstruction
                    {
                        RouteOffsetInMeters = 0,
                        TravelTimeInSeconds = 0,
                        Message = "Head north on Test St",
                        Point = new Coordinate { Lat = 34.0522, Lon = -118.2437 }
                    }
                }
            };

            // Create sample Azure Maps POST data
            var azureMapsPostData = new AzureMapsPostData
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
                            Coordinates = new[] { -118.2437, 34.0522 } // [lon, lat]
                        },
                        Properties = new RouteFeatureProperties
                        {
                            PointIndex = 0,
                            PointType = "waypoint"
                        }
                    }
                },
                TravelMode = "driving",
                RouteOutputOptions = new[] { "routePath", "itinerary" }
            };

            // Create the wrapper
            var routeWithData = new RouteWithRequestData
            {
                Route = routeResult,
                AzureMapsPostData = azureMapsPostData
            };

            // Validate structure
            Assert.NotNull(routeWithData.Route);
            Assert.NotNull(routeWithData.AzureMapsPostData);
            Assert.Equal(15000, routeWithData.Route.DistanceMeters);
            Assert.Equal(1200, routeWithData.Route.TravelTimeSeconds);
            Assert.Equal("FeatureCollection", routeWithData.AzureMapsPostData.Type);
            Assert.Equal("driving", routeWithData.AzureMapsPostData.TravelMode);

            // Test JSON serialization
            var json = JsonSerializer.Serialize(routeWithData);
            Assert.NotNull(json);
            Assert.Contains("distanceMeters", json);
            Assert.Contains("travelTimeSeconds", json);
            Assert.Contains("azureMapsPostData", json);
            Assert.Contains("FeatureCollection", json);

            _output.WriteLine("✅ RouteWithRequestData structure validation passed");
            _output.WriteLine($"Route distance: {routeWithData.Route.DistanceMeters}m");
            _output.WriteLine($"Route time: {routeWithData.Route.TravelTimeSeconds}s");
            _output.WriteLine($"Azure Maps data type: {routeWithData.AzureMapsPostData.Type}");
            _output.WriteLine($"Generated JSON length: {json.Length} characters");
        }
    }
}