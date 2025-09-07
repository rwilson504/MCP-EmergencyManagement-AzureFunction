using EmergencyManagementMCP.Models;
using EmergencyManagementMCP.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;

namespace EmergencyManagementMCP.Tests
{
    /// <summary>
    /// Test to reproduce and verify the California bounding box issue
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
            
            // Test the geometry parameter construction
            var geometryParam = $"{californiaBbox.MinLon},{californiaBbox.MinLat},{californiaBbox.MaxLon},{californiaBbox.MaxLat}";
            Console.WriteLine($"\nGeometry parameter should be: {geometryParam}");
            Console.WriteLine($"Expected: -125,32,-114,42");
            
            // Build the complete query parameters as they should appear
            var queryParams = new Dictionary<string, string>
            {
                ["where"] = "1=1",
                ["geometry"] = geometryParam,
                ["geometryType"] = "esriGeometryEnvelope",
                ["spatialRel"] = "esriSpatialRelIntersects",
                ["outFields"] = "*",
                ["returnGeometry"] = "true",
                ["f"] = "geojson",
                ["inSR"] = "4326"  // WGS 84 coordinate system
            };
            
            Console.WriteLine($"\nExpected query parameters:");
            foreach (var param in queryParams)
            {
                Console.WriteLine($"  {param.Key} = {param.Value}");
            }
            
            // Build the full URL
            var baseUrl = "https://services3.arcgis.com/T4QMspbfLg3qTGWY/ArcGIS/rest/services/WFIGS_Interagency_Perimeters_YearToDate/FeatureServer/0/query";
            var query = string.Join("&", queryParams.Select(kvp => 
                $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value)}"));
            var fullUrl = $"{baseUrl}?{query}";
            
            Console.WriteLine($"\nExpected full URL:");
            Console.WriteLine($"{fullUrl}");
            
            Console.WriteLine($"\n✅ California bounding box test completed!");
        }
    }
}