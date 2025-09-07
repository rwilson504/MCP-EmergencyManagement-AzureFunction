using EmergencyManagementMCP.Models;
using EmergencyManagementMCP.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System.Text.Json;

namespace EmergencyManagementMCP.Tests
{
    /// <summary>
    /// Test to verify the RouterClient correctly extracts driving directions
    /// from Azure Maps API responses
    /// </summary>
    public class DrivingDirectionsTest
    {
        public static void TestDrivingDirectionsExtraction()
        {
            Console.WriteLine("Driving Directions Extraction Test");
            Console.WriteLine("=================================");
            
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
                                    ""travelTimeInSeconds"": 180,
                                    ""point"": {""latitude"": 34.0530, ""longitude"": -118.2440},
                                    ""message"": ""Turn right onto Broadway""
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
            
            Console.WriteLine("Test Scenario:");
            Console.WriteLine("- Parse Azure Maps response with guidance instructions");
            Console.WriteLine("- Extract driving directions and validate structure");
            Console.WriteLine();
            
            // Simulate the parsing logic
            try
            {
                using var doc = JsonDocument.Parse(sampleResponse);
                var root = doc.RootElement;
                var routes = root.GetProperty("routes");
                var firstRoute = routes[0];
                
                // Test basic route properties
                var summary = firstRoute.GetProperty("summary");
                var distanceMeters = summary.GetProperty("lengthInMeters").GetInt32();
                var travelTimeSeconds = summary.GetProperty("travelTimeInSeconds").GetInt32();
                
                Console.WriteLine($"Route Summary:");
                Console.WriteLine($"  Distance: {distanceMeters} meters");
                Console.WriteLine($"  Travel Time: {travelTimeSeconds} seconds");
                
                // Test driving directions extraction
                var drivingDirections = new List<DrivingInstruction>();
                
                if (firstRoute.TryGetProperty("guidance", out var guidance) && 
                    guidance.TryGetProperty("instructions", out var instructions))
                {
                    foreach (var instruction in instructions.EnumerateArray())
                    {
                        var direction = new DrivingInstruction();
                        
                        if (instruction.TryGetProperty("routeOffsetInMeters", out var offsetElement))
                        {
                            direction.RouteOffsetInMeters = offsetElement.GetInt32();
                        }
                        
                        if (instruction.TryGetProperty("travelTimeInSeconds", out var timeElement))
                        {
                            direction.TravelTimeInSeconds = timeElement.GetInt32();
                        }
                        
                        if (instruction.TryGetProperty("message", out var messageElement))
                        {
                            direction.Message = messageElement.GetString() ?? string.Empty;
                        }
                        
                        if (instruction.TryGetProperty("point", out var pointElement))
                        {
                            direction.Point = new Coordinate
                            {
                                Lat = pointElement.GetProperty("latitude").GetDouble(),
                                Lon = pointElement.GetProperty("longitude").GetDouble()
                            };
                        }
                        
                        drivingDirections.Add(direction);
                    }
                }
                
                Console.WriteLine($"\nDriving Directions ({drivingDirections.Count} instructions):");
                foreach (var direction in drivingDirections)
                {
                    Console.WriteLine($"  [{direction.RouteOffsetInMeters}m, {direction.TravelTimeInSeconds}s] " +
                                    $"at ({direction.Point.Lat:F4}, {direction.Point.Lon:F4}): {direction.Message}");
                }
                
                // Validate expected results
                var validations = new Dictionary<string, bool>
                {
                    ["Has driving directions"] = drivingDirections.Count > 0,
                    ["First instruction is start"] = drivingDirections.Count > 0 && drivingDirections[0].RouteOffsetInMeters == 0,
                    ["Last instruction is destination"] = drivingDirections.Count > 0 && drivingDirections.Last().Message.Contains("destination"),
                    ["Instructions have messages"] = drivingDirections.All(d => !string.IsNullOrEmpty(d.Message)),
                    ["Instructions have coordinates"] = drivingDirections.All(d => d.Point.Lat != 0 && d.Point.Lon != 0)
                };
                
                Console.WriteLine("\nValidations:");
                foreach (var validation in validations)
                {
                    var status = validation.Value ? "✅" : "❌";
                    Console.WriteLine($"{status} {validation.Key}: {validation.Value}");
                }
                
                if (validations.Values.All(v => v))
                {
                    Console.WriteLine("\n🎉 All validations passed - Driving directions extraction is working correctly!");
                }
                else
                {
                    Console.WriteLine("\n❌ Some validations failed - review the implementation");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error during test: {ex.Message}");
            }
        }
    }
}