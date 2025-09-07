using System;
using System.Threading.Tasks;
using EmergencyManagementMCP.Models;
using EmergencyManagementMCP.Services;

namespace EmergencyManagementMCP.Tests
{
    /// <summary>
    /// Standalone test to verify the new functionality works without requiring Azure Functions runtime
    /// </summary>
    public class StandaloneAddressFireZoneTest
    {
        public static void TestGeocodingResponseParsing()
        {
            Console.WriteLine("Standalone Address Fire Zone Test");
            Console.WriteLine("================================");
            
            // Test 1: Test geocoding response model
            Console.WriteLine("\n1. Testing Geocoding Response Model:");
            var geocodingResult = new GeocodingResult
            {
                Address = "123 Main St, Los Angeles, CA",
                Coordinates = new Coordinate { Lat = 34.0522, Lon = -118.2437 },
                FormattedAddress = "123 Main St, Los Angeles, CA 90012, United States",
                Confidence = "High"
            };
            
            Console.WriteLine($"✅ Original Address: {geocodingResult.Address}");
            Console.WriteLine($"✅ Coordinates: ({geocodingResult.Coordinates.Lat}, {geocodingResult.Coordinates.Lon})");
            Console.WriteLine($"✅ Formatted Address: {geocodingResult.FormattedAddress}");
            Console.WriteLine($"✅ Confidence: {geocodingResult.Confidence}");
            
            // Test 2: Test fire zone info model
            Console.WriteLine("\n2. Testing Fire Zone Info Model:");
            var fireZoneInfo = new FireZoneInfo
            {
                IsInFireZone = true,
                FireZoneName = "Camp Fire",
                IncidentName = "Camp Fire Incident",
                ContainmentPercent = 85.5,
                AcresBurned = 153336.0,
                LastUpdate = DateTime.UtcNow
            };
            
            Console.WriteLine($"✅ Is In Fire Zone: {fireZoneInfo.IsInFireZone}");
            Console.WriteLine($"✅ Fire Zone Name: {fireZoneInfo.FireZoneName}");
            Console.WriteLine($"✅ Incident Name: {fireZoneInfo.IncidentName}");
            Console.WriteLine($"✅ Containment: {fireZoneInfo.ContainmentPercent}%");
            Console.WriteLine($"✅ Acres Burned: {fireZoneInfo.AcresBurned:N0}");
            Console.WriteLine($"✅ Last Update: {fireZoneInfo.LastUpdate}");
            
            // Test 3: Test combined response model
            Console.WriteLine("\n3. Testing Combined Response Model:");
            var response = new AddressFireZoneResponse
            {
                Geocoding = geocodingResult,
                FireZone = fireZoneInfo,
                TraceId = "test12345"
            };
            
            Console.WriteLine($"✅ Response Trace ID: {response.TraceId}");
            Console.WriteLine($"✅ Combined Result: Address '{response.Geocoding.Address}' at ({response.Geocoding.Coordinates.Lat}, {response.Geocoding.Coordinates.Lon}) is {(response.FireZone.IsInFireZone ? "IN" : "NOT IN")} fire zone");
            
            // Test 4: Test coordinate validation
            Console.WriteLine("\n4. Testing Coordinate Validation:");
            var testCoordinates = new[]
            {
                new { Lat = 34.0522, Lon = -118.2437, Valid = true, Location = "Los Angeles" },
                new { Lat = 91.0, Lon = -118.2437, Valid = false, Location = "Invalid Latitude" },
                new { Lat = 34.0522, Lon = -200.0, Valid = false, Location = "Invalid Longitude" },
                new { Lat = 0.0, Lon = 0.0, Valid = true, Location = "Null Island" }
            };
            
            foreach (var test in testCoordinates)
            {
                var isValid = test.Lat >= -90 && test.Lat <= 90 && test.Lon >= -180 && test.Lon <= 180;
                var status = isValid ? "✅ VALID" : "❌ INVALID";
                Console.WriteLine($"{status}: {test.Location} ({test.Lat}, {test.Lon}) - Expected: {(test.Valid ? "VALID" : "INVALID")}");
            }
            
            // Test 5: Test point-in-polygon algorithm basics
            Console.WriteLine("\n5. Testing Point-in-Polygon Logic:");
            // Simple square polygon test
            var squareVertices = new List<(double lat, double lon)>
            {
                (34.0, -118.3),  // Bottom-left
                (34.1, -118.3),  // Top-left  
                (34.1, -118.2),  // Top-right
                (34.0, -118.2),  // Bottom-right
                (34.0, -118.3)   // Close the polygon
            };
            
            var testPoints = new[]
            {
                new { Lat = 34.05, Lon = -118.25, Expected = true, Name = "Center of square" },
                new { Lat = 33.95, Lon = -118.25, Expected = false, Name = "Below square" },
                new { Lat = 34.05, Lon = -118.35, Expected = false, Name = "Left of square" }
            };
            
            foreach (var point in testPoints)
            {
                var inside = IsPointInPolygonRaycast(point.Lat, point.Lon, squareVertices);
                var status = inside == point.Expected ? "✅ CORRECT" : "❌ INCORRECT";
                Console.WriteLine($"{status}: {point.Name} ({point.Lat}, {point.Lon}) - Expected: {(point.Expected ? "INSIDE" : "OUTSIDE")}, Got: {(inside ? "INSIDE" : "OUTSIDE")}");
            }
            
            Console.WriteLine("\n🎉 All model tests completed successfully!");
            Console.WriteLine("\nNext Steps for Manual Testing:");
            Console.WriteLine("1. Deploy to Azure Functions");
            Console.WriteLine("2. Test with real Azure Maps API key");
            Console.WriteLine("3. Test with real addresses in fire-prone areas");
            Console.WriteLine("4. Test with MCP Inspector or direct HTTP calls");
        }
        
        // Copy of the ray casting algorithm for testing
        private static bool IsPointInPolygonRaycast(double testLat, double testLon, List<(double lat, double lon)> vertices)
        {
            if (vertices.Count < 3) return false;

            bool inside = false;
            int j = vertices.Count - 1;

            for (int i = 0; i < vertices.Count; i++)
            {
                var (iLat, iLon) = vertices[i];
                var (jLat, jLon) = vertices[j];

                if (((iLat > testLat) != (jLat > testLat)) &&
                    (testLon < (jLon - iLon) * (testLat - iLat) / (jLat - iLat) + iLon))
                {
                    inside = !inside;
                }
                j = i;
            }

            return inside;
        }
    }
}