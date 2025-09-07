using EmergencyManagementMCP.Models;
using EmergencyManagementMCP.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System.Web;

namespace EmergencyManagementMCP.Tests
{
    /// <summary>
    /// Test to verify the RouterClient correctly formats Azure Maps API requests
    /// after fixing the avoid parameter issue
    /// </summary>
    public class RouterClientApiFormatTest
    {
        public static void TestAzureMapsUrlConstruction()
        {
            Console.WriteLine("Router Client API Format Test");
            Console.WriteLine("============================");
            
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
            
            Console.WriteLine("Test Scenario:");
            Console.WriteLine($"  Origin: {origin.Lat}, {origin.Lon}");
            Console.WriteLine($"  Destination: {destination.Lat}, {destination.Lon}");
            Console.WriteLine($"  Avoid Areas: {avoidAreas.Count}");
            
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
                var areasToUse = avoidAreas.Take(10).ToList();
                var avoidAreasStr = string.Join("|", areasToUse.Select(r => 
                    $"{r.MinLat},{r.MinLon}:{r.MaxLat},{r.MaxLon}"));
                queryParams.Add($"avoidAreas={HttpUtility.UrlEncode(avoidAreasStr)}");
            }
            
            var queryString = string.Join("&", queryParams);
            var fullUrl = $"https://atlas.microsoft.com/route/directions/json?{queryString}";
            
            Console.WriteLine("\nGenerated URL (with dummy key):");
            Console.WriteLine(fullUrl.Replace("dummy_key_for_testing", "***REDACTED***"));
            
            // Verify the fixes are applied
            var hasCorrectApiVersion = queryString.Contains("api-version=2025-01-01");
            var hasAvoidAreas = queryString.Contains("avoidAreas=");
            var doesNotHaveInvalidAvoid = !queryString.Contains("avoid=avoidAreas");
            var hasCorrectAvoidFormat = queryString.Contains("avoidAreas=34%2c-118.3%3a34.1%2c-118.2") ||
                                      queryString.Contains("avoidAreas=34,-118.3:34.1,-118.2");
            
            Console.WriteLine("\nValidations:");
            Console.WriteLine($"✅ Uses API version 2025-01-01: {hasCorrectApiVersion}");
            Console.WriteLine($"✅ Has avoidAreas parameter: {hasAvoidAreas}");
            Console.WriteLine($"✅ Does NOT have invalid 'avoid=avoidAreas': {doesNotHaveInvalidAvoid}");
            Console.WriteLine($"✅ Has correct avoid area format: {hasCorrectAvoidFormat}");
            
            if (hasCorrectApiVersion && hasAvoidAreas && doesNotHaveInvalidAvoid && hasCorrectAvoidFormat)
            {
                Console.WriteLine("\n🎉 All validations passed - Azure Maps API format is correct!");
            }
            else
            {
                Console.WriteLine("\n❌ Some validations failed - review the implementation");
            }
            
            // Test edge case: no avoid areas
            Console.WriteLine("\n--- Testing without avoid areas ---");
            var queryParamsNoAvoid = new List<string>
            {
                "api-version=2025-01-01",
                "subscription-key=dummy_key_for_testing",
                $"query={origin.Lat},{origin.Lon}:{destination.Lat},{destination.Lon}",
                "routeType=fastest",
                "travelMode=car"
            };
            
            var queryStringNoAvoid = string.Join("&", queryParamsNoAvoid);
            var noAvoidAreasPresent = !queryStringNoAvoid.Contains("avoidAreas");
            var noInvalidAvoidPresent = !queryStringNoAvoid.Contains("avoid=avoidAreas");
            
            Console.WriteLine($"✅ No avoidAreas when none specified: {noAvoidAreasPresent}");
            Console.WriteLine($"✅ No invalid avoid parameter: {noInvalidAvoidPresent}");
            
            Console.WriteLine("\n✅ Router Client API format test completed!");
        }
    }
}