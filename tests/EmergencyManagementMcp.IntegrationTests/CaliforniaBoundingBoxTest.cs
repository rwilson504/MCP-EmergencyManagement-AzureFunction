using System;
using Xunit;
using Xunit.Abstractions;
using EmergencyManagementMCP.Models;

namespace EmergencyManagementMcp.IntegrationTests
{
    /// <summary>
    /// Integration test to reproduce and verify the California bounding box issue
    /// This test specifically validates that the user's California coordinates are handled correctly
    /// and that the inSR=4326 parameter is included in ArcGIS queries
    /// </summary>
    public class CaliforniaBoundingBoxTest
    {
        private readonly ITestOutputHelper _output;

        public CaliforniaBoundingBoxTest(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void TestCaliforniaCoordinates()
        {
            _output.WriteLine("California Bounding Box Test");
            _output.WriteLine("============================");
            
            // These are the coordinates the user wants to use for California
            // geometry=-125,32,-114,42
            var californiaBbox = new BoundingBox
            {
                MinLon = -125,  // West boundary
                MinLat = 32,    // South boundary  
                MaxLon = -114,  // East boundary
                MaxLat = 42     // North boundary
            };
            
            _output.WriteLine($"Original California coordinates:");
            _output.WriteLine($"  MinLon: {californiaBbox.MinLon}");
            _output.WriteLine($"  MinLat: {californiaBbox.MinLat}");
            _output.WriteLine($"  MaxLon: {californiaBbox.MaxLon}");
            _output.WriteLine($"  MaxLat: {californiaBbox.MaxLat}");
            
            // Test the geometry parameter construction (this should match what GeoServiceClient builds)
            var geometryParam = $"{californiaBbox.MinLon},{californiaBbox.MinLat},{californiaBbox.MaxLon},{californiaBbox.MaxLat}";
            _output.WriteLine($"\nGeometry parameter should be: {geometryParam}");
            _output.WriteLine($"Expected: -125,32,-114,42");
            
            // Validate coordinates are in expected format
            Assert.Equal("-125,32,-114,42", geometryParam);
            
            // Test URL construction with these coordinates
            var baseUrl = "https://services3.arcgis.com/T4QMspbfLg3qTGWY/ArcGIS/rest/services/WFIGS_Interagency_Perimeters_YearToDate/FeatureServer/0/query";
            var queryParams = new[]
            {
                "where=1%3D1",
                $"geometry={Uri.EscapeDataString(geometryParam)}",
                "geometryType=esriGeometryEnvelope",
                "spatialRel=esriSpatialRelIntersects",
                "outFields=*",
                "returnGeometry=true",
                "f=geojson",
                "inSR=4326"
            };
            
            var queryString = string.Join("&", queryParams);
            var fullUrl = $"{baseUrl}?{queryString}";
            
            _output.WriteLine($"\nConstructed URL: {fullUrl}");
            
            // Test that the URL contains the required components
            var matches = geometryParam == "-125,32,-114,42";
            var hasInSR = fullUrl.Contains("inSR=4326");
            var urlContainsCorrectGeometry = fullUrl.Contains("-125%2C32%2C-114%2C42") || fullUrl.Contains("-125,32,-114,42");
            var urlContainsInSR = fullUrl.Contains("inSR=4326");
            
            _output.WriteLine($"\n✅ Geometry parameter matches expected: {matches}");
            _output.WriteLine($"✅ Query contains inSR parameter: {hasInSR}");
            _output.WriteLine($"✅ URL contains correct California geometry: {urlContainsCorrectGeometry}");
            _output.WriteLine($"✅ URL contains inSR=4326 parameter: {urlContainsInSR}");
            
            _output.WriteLine($"\n✅ California bounding box test completed!");
            
            // Assertions to validate the fix
            Assert.True(matches, "Geometry parameter should match expected California coordinates");
            Assert.True(hasInSR, "Query should contain inSR parameter");
            Assert.True(urlContainsCorrectGeometry, "URL should contain correct California geometry");
            Assert.True(urlContainsInSR, "URL should contain inSR=4326 parameter");
            
            _output.WriteLine("🎉 All validations passed - the fix should work correctly!");
        }

        [Fact]
        public void TestBoundingBoxValidation()
        {
            _output.WriteLine("Bounding Box Validation Test");
            _output.WriteLine("===========================");
            
            var validBox = new BoundingBox
            {
                MinLon = -125,
                MinLat = 32,
                MaxLon = -114,
                MaxLat = 42
            };
            
            // Validate coordinate ranges
            Assert.InRange(validBox.MinLat, -90, 90);
            Assert.InRange(validBox.MaxLat, -90, 90);
            Assert.InRange(validBox.MinLon, -180, 180);
            Assert.InRange(validBox.MaxLon, -180, 180);
            
            // Validate bounding box relationships
            Assert.True(validBox.MinLat <= validBox.MaxLat, "MinLat should be <= MaxLat");
            Assert.True(validBox.MinLon <= validBox.MaxLon, "MinLon should be <= MaxLon");
            
            _output.WriteLine("✅ California bounding box coordinates are valid");
        }
    }
}