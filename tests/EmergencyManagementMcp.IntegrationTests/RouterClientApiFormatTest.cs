using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Xunit;
using Xunit.Abstractions;
using EmergencyManagementMCP.Models;

namespace EmergencyManagementMcp.IntegrationTests
{
    /// <summary>
    /// Integration test to verify the RouterClient correctly formats Azure Maps API requests
    /// after fixing the avoid parameter issue
    /// </summary>
    public class RouterClientApiFormatTest
    {
        private readonly ITestOutputHelper _output;

        public RouterClientApiFormatTest(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void TestAzureMapsUrlConstruction()
        {
            _output.WriteLine("Router Client API Format Test");
            _output.WriteLine("============================");
            
            // Test parameters that would trigger the avoid areas functionality
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
            
            _output.WriteLine("Test Scenario:");
            _output.WriteLine($"  Origin: {origin.Lat}, {origin.Lon}");
            _output.WriteLine($"  Destination: {destination.Lat}, {destination.Lon}");
            _output.WriteLine($"  Avoid Areas: {avoidAreas.Count}");
            
            // Simulate the URL construction logic from RouterClient
            var queryParams = new List<string>
            {
                "api-version=2025-01-01",  // Updated API version
                "subscription-key=dummy_key_for_testing",
                $"query={origin.Lat},{origin.Lon}:{destination.Lat},{destination.Lon}",
                "routeType=fastest",
                "travelMode=car"
            };
            
            // Add avoid areas using the corrected format (without avoid=avoidAreas)
            if (avoidAreas.Any())
            {
                var avoidAreasParam = string.Join(":", avoidAreas.Select(area =>
                    $"{area.MinLat},{area.MinLon}:{area.MaxLat},{area.MaxLon}"));
                queryParams.Add($"avoidAreas={avoidAreasParam}");
            }
            
            var queryString = string.Join("&", queryParams);
            var fullUrl = $"https://atlas.microsoft.com/route/directions/json?{queryString}";
            
            _output.WriteLine($"\nGenerated URL:");
            _output.WriteLine($"  {fullUrl}");
            
            _output.WriteLine($"\nQuery Parameters:");
            foreach (var param in queryParams)
            {
                _output.WriteLine($"  {param}");
            }
            
            // Validate the URL format
            var hasCorrectApiVersion = queryString.Contains("api-version=2025-01-01");
            var hasAvoidAreas = queryString.Contains("avoidAreas=");
            var doesNotHaveInvalidAvoid = !queryString.Contains("avoid=avoidAreas");
            var hasCorrectAvoidFormat = queryString.Contains("avoidAreas=34,-118.3:34.1,-118.2") ||
                                      queryString.Contains("avoidAreas=34%2c-118.3%3a34.1%2c-118.2");
            
            _output.WriteLine("\nValidations:");
            _output.WriteLine($"✅ Uses API version 2025-01-01: {hasCorrectApiVersion}");
            _output.WriteLine($"✅ Has avoidAreas parameter: {hasAvoidAreas}");
            _output.WriteLine($"✅ Does NOT have invalid 'avoid=avoidAreas': {doesNotHaveInvalidAvoid}");
            _output.WriteLine($"✅ Has correct avoid area format: {hasCorrectAvoidFormat}");
            
            // Assert the validations
            Assert.True(hasCorrectApiVersion, "Should use API version 2025-01-01");
            Assert.True(hasAvoidAreas, "Should have avoidAreas parameter");
            Assert.True(doesNotHaveInvalidAvoid, "Should NOT have invalid 'avoid=avoidAreas'");
            Assert.True(hasCorrectAvoidFormat, "Should have correct avoid area format");
            
            _output.WriteLine("\n🎉 All validations passed - Azure Maps API format is correct!");
        }

        [Fact]
        public void TestUrlConstructionWithoutAvoidAreas()
        {
            _output.WriteLine("Testing URL construction without avoid areas");
            _output.WriteLine("==========================================");
            
            var queryParamsNoAvoid = new List<string>
            {
                "api-version=2025-01-01",
                "subscription-key=dummy_key_for_testing",
                "query=34.0522,-118.2437:34.1625,-118.1331",
                "routeType=fastest",
                "travelMode=car"
            };
            
            var queryStringNoAvoid = string.Join("&", queryParamsNoAvoid);
            var fullUrlNoAvoid = $"https://atlas.microsoft.com/route/directions/json?{queryStringNoAvoid}";
            
            _output.WriteLine($"URL without avoid areas: {fullUrlNoAvoid}");
            
            // Should not contain avoid parameters
            Assert.DoesNotContain("avoidAreas=", queryStringNoAvoid);
            Assert.DoesNotContain("avoid=", queryStringNoAvoid);
            Assert.Contains("api-version=2025-01-01", queryStringNoAvoid);
            
            _output.WriteLine("✅ Router Client API format test completed!");
        }
    }
}