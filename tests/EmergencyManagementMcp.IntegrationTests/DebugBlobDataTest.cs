using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;
using Xunit.Abstractions;
using EmergencyManagementMCP.Models;

namespace EmergencyManagementMcp.IntegrationTests
{
    /// <summary>
    /// Test to debug the exact blob data format causing the MapPage.tsx issue
    /// </summary>
    public class DebugBlobDataTest
    {
        private readonly ITestOutputHelper _output;

        public DebugBlobDataTest(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void AnalyzeEmptyBlobData()
        {
            _output.WriteLine("=== Debugging Blob Data Issue ===");
            
            // The problematic data from the issue description
            var problematicJson = @"{
                ""type"": ""FeatureCollection"",
                ""features"": [],
                ""avoidAreas"": null,
                ""routeOutputOptions"": [""routePath""],
                ""travelMode"": ""driving""
            }";

            _output.WriteLine("Problematic JSON from issue:");
            _output.WriteLine(problematicJson);
            
            // Parse it to see what we get
            var parsed = JsonSerializer.Deserialize<AzureMapsPostData>(problematicJson);
            
            Assert.NotNull(parsed);
            Assert.Equal("FeatureCollection", parsed.Type);
            Assert.Equal("driving", parsed.TravelMode);
            Assert.Empty(parsed.Features); // This is the problem!
            Assert.Null(parsed.AvoidAreas); // This is also the problem!
            
            _output.WriteLine($"Parsed Type: {parsed.Type}");
            _output.WriteLine($"Parsed TravelMode: {parsed.TravelMode}");
            _output.WriteLine($"Parsed Features Count: {parsed.Features.Length}");
            _output.WriteLine($"Parsed AvoidAreas: {(parsed.AvoidAreas == null ? "null" : "present")}");
            _output.WriteLine($"Parsed RouteOutputOptions: [{string.Join(", ", parsed.RouteOutputOptions)}]");

            // Now let's create what SHOULD be in the blob
            var correctData = new AzureMapsPostData
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
                            Coordinates = new[] { -118.2437, 34.0522 } // LA
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
                            Coordinates = new[] { -118.1331, 34.1625 } // Pasadena  
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
                AvoidAreas = new MultiPolygon
                {
                    Type = "MultiPolygon",
                    Coordinates = new[]
                    {
                        new[]
                        {
                            new[]
                            {
                                new[] { -118.3, 34.0 },
                                new[] { -118.2, 34.0 },
                                new[] { -118.2, 34.1 },
                                new[] { -118.3, 34.1 },
                                new[] { -118.3, 34.0 } // Closed polygon
                            }
                        }
                    }
                }
            };

            var correctJson = JsonSerializer.Serialize(correctData, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            });
            
            _output.WriteLine("");
            _output.WriteLine("What the blob SHOULD contain:");
            _output.WriteLine(correctJson);
            
            Assert.Equal(2, correctData.Features.Length);
            Assert.NotNull(correctData.AvoidAreas);
            Assert.Equal("MultiPolygon", correctData.AvoidAreas.Type);
            
            _output.WriteLine("");
            _output.WriteLine("✅ Identified the root cause:");
            _output.WriteLine("  - Current blob has empty features[] array");
            _output.WriteLine("  - Current blob has null avoidAreas");
            _output.WriteLine("  - Should have 2 waypoint features and MultiPolygon avoid areas");
        }
    }
}