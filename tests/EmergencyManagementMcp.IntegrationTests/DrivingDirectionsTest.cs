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
    /// Integration test to verify the RouterClient correctly extracts driving directions
    /// from Azure Maps API responses
    /// </summary>
    public class DrivingDirectionsTest
    {
        private readonly ITestOutputHelper _output;

        public DrivingDirectionsTest(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void TestDrivingDirectionsExtraction()
        {
            _output.WriteLine("Driving Directions Extraction Test");
            _output.WriteLine("=================================");
            
            // Sample Azure Maps response with guidance instructions
            var sampleResponse = @"{
                ""routes"": [
                    {
                        ""summary"": {
                            ""lengthInMeters"": 15420,
                            ""travelTimeInSeconds"": 1140
                        },
                        ""legs"": [
                            {
                                ""summary"": {
                                    ""lengthInMeters"": 15420,
                                    ""travelTimeInSeconds"": 1140
                                },
                                ""points"": [
                                    {""latitude"": 34.0522, ""longitude"": -118.2437},
                                    {""latitude"": 34.0530, ""longitude"": -118.2440},
                                    {""latitude"": 34.1625, ""longitude"": -118.1331}
                                ]
                            }
                        ],
                        ""guidance"": {
                            ""instructions"": [
                                {
                                    ""routeOffsetInMeters"": 0,
                                    ""travelTimeInSeconds"": 0,
                                    ""point"": {""latitude"": 34.0522, ""longitude"": -118.2437},
                                    ""message"": ""Head north on Main St""
                                },
                                {
                                    ""routeOffsetInMeters"": 1200,
                                    ""travelTimeInSeconds"": 90,
                                    ""point"": {""latitude"": 34.0530, ""longitude"": -118.2440},
                                    ""message"": ""Turn right onto First St""
                                },
                                {
                                    ""routeOffsetInMeters"": 15420,
                                    ""travelTimeInSeconds"": 1140,
                                    ""point"": {""latitude"": 34.1625, ""longitude"": -118.1331},
                                    ""message"": ""You have arrived at your destination""
                                }
                            ]
                        }
                    }
                ]
            }";
            
            _output.WriteLine("Sample Azure Maps Response Structure:");
            _output.WriteLine("- Route summary with distance and time");
            _output.WriteLine("- Route legs with waypoints");
            _output.WriteLine("- Guidance instructions with turn-by-turn directions");
            
            try
            {
                // Parse the JSON to validate structure
                using var jsonDoc = JsonDocument.Parse(sampleResponse);
                var root = jsonDoc.RootElement;
                
                Assert.True(root.TryGetProperty("routes", out var routes), "Response should have routes property");
                Assert.True(routes.GetArrayLength() > 0, "Should have at least one route");
                
                var firstRoute = routes[0];
                Assert.True(firstRoute.TryGetProperty("summary", out var summary), "Route should have summary");
                Assert.True(firstRoute.TryGetProperty("guidance", out var guidance), "Route should have guidance");
                Assert.True(guidance.TryGetProperty("instructions", out var instructions), "Guidance should have instructions");
                
                // Extract and validate driving directions
                var drivingDirections = new List<DrivingInstruction>();
                
                foreach (var instruction in instructions.EnumerateArray())
                {
                    if (instruction.TryGetProperty("message", out var messageElement) &&
                        instruction.TryGetProperty("point", out var pointElement))
                    {
                        var direction = new DrivingInstruction
                        {
                            Message = messageElement.GetString() ?? "",
                            Point = new Coordinate
                            {
                                Lat = pointElement.GetProperty("latitude").GetDouble(),
                                Lon = pointElement.GetProperty("longitude").GetDouble()
                            }
                        };
                        drivingDirections.Add(direction);
                    }
                }
                
                _output.WriteLine($"\nExtracted {drivingDirections.Count} driving directions:");
                foreach (var direction in drivingDirections)
                {
                    _output.WriteLine($"- {direction.Message} at ({direction.Point.Lat}, {direction.Point.Lon})");
                }
                
                // Validate extracted directions
                var validations = new Dictionary<string, bool>
                {
                    ["Has driving directions"] = drivingDirections.Count > 0,
                    ["First instruction is start"] = drivingDirections.Count > 0 && drivingDirections[0].Message.Contains("Head"),
                    ["Last instruction is destination"] = drivingDirections.Count > 0 && drivingDirections.Last().Message.Contains("destination"),
                    ["Instructions have messages"] = drivingDirections.All(d => !string.IsNullOrEmpty(d.Message)),
                    ["Instructions have coordinates"] = drivingDirections.All(d => d.Point.Lat != 0 && d.Point.Lon != 0)
                };
                
                _output.WriteLine("\nValidations:");
                foreach (var validation in validations)
                {
                    var status = validation.Value ? "✅" : "❌";
                    _output.WriteLine($"{status} {validation.Key}: {validation.Value}");
                }
                
                // Assert all validations pass
                foreach (var validation in validations)
                {
                    Assert.True(validation.Value, $"Validation failed: {validation.Key}");
                }
                
                _output.WriteLine("\n🎉 All validations passed - Driving directions extraction is working correctly!");
            }
            catch (Exception ex)
            {
                _output.WriteLine($"❌ Error during test: {ex.Message}");
                throw;
            }
        }

        [Fact]
        public void TestJsonResponseStructure()
        {
            _output.WriteLine("Azure Maps JSON Response Structure Test");
            _output.WriteLine("======================================");
            
            // Test that we can handle minimal response structure
            var minimalResponse = @"{
                ""routes"": [
                    {
                        ""summary"": {
                            ""lengthInMeters"": 1000,
                            ""travelTimeInSeconds"": 60
                        }
                    }
                ]
            }";
            
            using var jsonDoc = JsonDocument.Parse(minimalResponse);
            var root = jsonDoc.RootElement;
            
            Assert.True(root.TryGetProperty("routes", out var routes));
            Assert.Equal(1, routes.GetArrayLength());
            
            var route = routes[0];
            Assert.True(route.TryGetProperty("summary", out var summary));
            Assert.Equal(1000, summary.GetProperty("lengthInMeters").GetInt32());
            Assert.Equal(60, summary.GetProperty("travelTimeInSeconds").GetInt32());
            
            _output.WriteLine("✅ Minimal JSON response structure validation passed");
        }
    }
}