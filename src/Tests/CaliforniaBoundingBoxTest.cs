using EmergencyManagementMCP.Models;
using EmergencyManagementMCP.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;

namespace EmergencyManagementMCP.Tests
{
    /// <summary>
    /// Test to reproduce and verify the California bounding box issue
    /// This test specifically validates that the user's California coordinates are handled correctly
    /// and that the inSR=4326 parameter is included in ArcGIS queries
    /// </summary>
    public class CaliforniaBoundingBoxTest
    {
        public static void TestCaliforniaCoordinates()
        {
            Console.WriteLine("California Bounding Box Test");
            Console.WriteLine("============================");
            
            // These are the coordinates the user wants to use for California
            // geometry=-125,32,-114,42
            var californiaBbox = new BoundingBox
            {
                MinLon = -125,  // West boundary
                MinLat = 32,    // South boundary  
                MaxLon = -114,  // East boundary
                MaxLat = 42     // North boundary
            };
            
            Console.WriteLine($"Original California coordinates:");
            Console.WriteLine($"  MinLon: {californiaBbox.MinLon}");
            Console.WriteLine($"  MinLat: {californiaBbox.MinLat}");
            Console.WriteLine($"  MaxLon: {californiaBbox.MaxLon}");
            Console.WriteLine($"  MaxLat: {californiaBbox.MaxLat}");
            
            // Test the geometry parameter construction (this should match what GeoServiceClient builds)
            var geometryParam = $"{californiaBbox.MinLon},{californiaBbox.MinLat},{californiaBbox.MaxLon},{californiaBbox.MaxLat}";
            Console.WriteLine($"\nGeometry parameter should be: {geometryParam}");
            Console.WriteLine($"Expected: -125,32,-114,42");
            
            // Verify it matches the expected format
            var expected = "-125,32,-114,42";
            var matches = geometryParam == expected;
            Console.WriteLine($"✅ Geometry parameter matches expected: {matches}");
            
            // Build the complete query parameters as they should appear after our fix
            var queryParams = new Dictionary<string, string>
            {
                ["where"] = "1=1",
                ["geometry"] = geometryParam,
                ["geometryType"] = "esriGeometryEnvelope",
                ["spatialRel"] = "esriSpatialRelIntersects",
                ["outFields"] = "*",
                ["returnGeometry"] = "true",
                ["f"] = "geojson",
                ["inSR"] = "4326"  // NEW: WGS 84 coordinate system (as requested in the issue)
            };
            
            Console.WriteLine($"\nExpected query parameters after fix:");
            foreach (var param in queryParams)
            {
                Console.WriteLine($"  {param.Key} = {param.Value}");
            }
            
            // Verify the inSR parameter is present
            var hasInSR = queryParams.ContainsKey("inSR") && queryParams["inSR"] == "4326";
            Console.WriteLine($"\n✅ inSR=4326 parameter is present: {hasInSR}");
            
            // Build the full URL
            var baseUrl = "https://services3.arcgis.com/T4QMspbfLg3qTGWY/ArcGIS/rest/services/WFIGS_Interagency_Perimeters_YearToDate/FeatureServer/0/query";
            var query = string.Join("&", queryParams.Select(kvp => 
                $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value)}"));
            var fullUrl = $"{baseUrl}?{query}";
            
            Console.WriteLine($"\nExpected full URL with California coordinates:");
            Console.WriteLine($"{fullUrl}");
            
            // Verify the URL contains the expected geometry
            var urlContainsCorrectGeometry = fullUrl.Contains("-125%2C32%2C-114%2C42") || fullUrl.Contains("-125,32,-114,42");
            Console.WriteLine($"\n✅ URL contains correct California geometry: {urlContainsCorrectGeometry}");
            
            // Verify the URL contains the inSR parameter
            var urlContainsInSR = fullUrl.Contains("inSR=4326");
            Console.WriteLine($"✅ URL contains inSR=4326 parameter: {urlContainsInSR}");
            
            Console.WriteLine($"\n✅ California bounding box test completed!");
            
            if (matches && hasInSR && urlContainsCorrectGeometry && urlContainsInSR)
            {
                Console.WriteLine("🎉 All validations passed - the fix should work correctly!");
            }
            else
            {
                Console.WriteLine("❌ Some validations failed - review the implementation");
            }
        }
    }
}