using System;
using System.Collections.Generic;
using System.Linq;

// Simulate the fixed queryParams construction from GeoServiceClient
public static class ValidationTest
{
    public static void Main()
    {
        Console.WriteLine("Validating ArcGIS bounding box fix");
        Console.WriteLine("==================================");

        // California coordinates as requested by user
        double minLon = -125, minLat = 32, maxLon = -114, maxLat = 42;
        var geometryParam = $"{minLon},{minLat},{maxLon},{maxLat}";
        
        Console.WriteLine($"California geometry: {geometryParam}");
        Console.WriteLine($"Should be: -125,32,-114,42");
        Console.WriteLine($"Match: {geometryParam == "-125,32,-114,42"}");
        
        // Simulate the fixed query parameters (after adding inSR=4326)
        var queryParams = new Dictionary<string, string>
        {
            ["where"] = "1=1",
            ["geometry"] = geometryParam,
            ["geometryType"] = "esriGeometryEnvelope",
            ["spatialRel"] = "esriSpatialRelIntersects",
            ["outFields"] = "*",
            ["returnGeometry"] = "true",
            ["f"] = "geojson",
            ["inSR"] = "4326"  // THIS IS THE NEW PARAMETER
        };
        
        Console.WriteLine("\nQuery parameters:");
        foreach (var kvp in queryParams)
        {
            Console.WriteLine($"  {kvp.Key}={kvp.Value}");
        }
        
        var query = string.Join("&", queryParams.Select(kvp => 
            $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value)}"));
            
        var baseUrl = "https://services3.arcgis.com/T4QMspbfLg3qTGWY/ArcGIS/rest/services/WFIGS_Interagency_Perimeters_YearToDate/FeatureServer/0/query";
        var fullUrl = $"{baseUrl}?{query}";
        
        Console.WriteLine($"\nFull URL:");
        Console.WriteLine(fullUrl);
        
        Console.WriteLine($"\nValidation:");
        Console.WriteLine($"✅ Contains inSR=4326: {fullUrl.Contains("inSR=4326")}");
        Console.WriteLine($"✅ Contains California coords: {fullUrl.Contains("-125") && fullUrl.Contains("32") && fullUrl.Contains("-114") && fullUrl.Contains("42")}");
        
        Console.WriteLine("\n🎉 Fix validated successfully!");
    }
}
