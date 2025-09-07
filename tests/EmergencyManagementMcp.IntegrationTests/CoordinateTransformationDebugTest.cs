using System;
using Xunit;
using Xunit.Abstractions;
using EmergencyManagementMCP.Models;
using EmergencyManagementMCP.Services;
using Microsoft.Extensions.Logging;

namespace EmergencyManagementMcp.IntegrationTests
{
    /// <summary>
    /// Integration test to investigate the coordinate transformation issue mentioned in the GitHub issue.
    /// The user reported that California coordinates (-125,32,-114,42) were transformed to
    /// unexpected values (29.98198198198198,32.98198198198198,33.01801801801802,33.01801801801802)
    /// </summary>
    public class CoordinateTransformationDebugTest
    {
        private readonly ITestOutputHelper _output;

        public CoordinateTransformationDebugTest(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void TestCoordinateTransformation()
        {
            _output.WriteLine("Coordinate Transformation Debug Test");
            _output.WriteLine("=====================================");
            
            // Create a mock logger for testing
            var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));
            var logger = loggerFactory.CreateLogger<GeometryUtils>();
            
            var geometryUtils = new GeometryUtils(logger);
            
            _output.WriteLine("Testing various scenarios that might produce the unexpected coordinates...");
            
            // Test Case 1: Small area around (32, 33) with different buffer sizes
            _output.WriteLine("\n--- Test Case 1: Small area with buffers ---");
            var origin1 = new Coordinate { Lat = 32, Lon = 32 };
            var destination1 = new Coordinate { Lat = 33, Lon = 33 };
            
            bool foundMatch = false;
            for (double bufferKm = 0; bufferKm <= 100; bufferKm += 10)
            {
                var bbox1 = geometryUtils.ComputeBBox(origin1, destination1, bufferKm);
                var geometryParam1 = $"{bbox1.MinLon},{bbox1.MinLat},{bbox1.MaxLon},{bbox1.MaxLat}";
                _output.WriteLine($"  Buffer {bufferKm}km: {geometryParam1}");
                
                // Check if this matches the unexpected coordinates
                if (Math.Abs(bbox1.MinLon - 29.98198198198198) < 0.001)
                {
                    _output.WriteLine($"    ⚠️  MATCH FOUND! Buffer {bufferKm}km produces similar coordinates");
                    foundMatch = true;
                }
            }
            
            if (!foundMatch)
            {
                _output.WriteLine("  No exact match found for unexpected coordinates");
            }
            
            // Test Case 2: Different coordinate pairs that might produce the unexpected result
            _output.WriteLine("\n--- Test Case 2: Testing coordinate pairs around 32-33 range ---");
            var testCoords = new[]
            {
                (32.0, 33.0, 0.0),   // No buffer
                (32.5, 33.5, 1.0),  // Small buffer
                (31.0, 34.0, 50.0), // Larger area with buffer
                (32.1, 32.9, 18.0)  // Different configuration
            };
            
            foreach (var (lat, lon, buffer) in testCoords)
            {
                var origin = new Coordinate { Lat = lat, Lon = lon };
                var destination = new Coordinate { Lat = lat + 1, Lon = lon + 1 };
                var bbox = geometryUtils.ComputeBBox(origin, destination, buffer);
                var geometryParam = $"{bbox.MinLon},{bbox.MinLat},{bbox.MaxLon},{bbox.MaxLat}";
                _output.WriteLine($"  Coords ({lat},{lon}) + {buffer}km buffer: {geometryParam}");
            }
            
            // Test Case 3: Reverse engineer the buffer calculation
            _output.WriteLine("\n--- Test Case 3: Reverse engineering the buffer calculation ---");
            var unexpectedCoords = (MinLon: 29.98198198198198, MinLat: 32.98198198198198, MaxLon: 33.01801801801802, MaxLat: 33.01801801801802);
            
            // Calculate the center and buffer from the unexpected coordinates
            var centerLat = (unexpectedCoords.MinLat + unexpectedCoords.MaxLat) / 2;
            var centerLon = (unexpectedCoords.MinLon + unexpectedCoords.MaxLon) / 2;
            var latBuffer = (unexpectedCoords.MaxLat - unexpectedCoords.MinLat) / 2;
            var lonBuffer = (unexpectedCoords.MaxLon - unexpectedCoords.MinLon) / 2;
            
            _output.WriteLine($"  Unexpected coordinates center: ({centerLon:F6}, {centerLat:F6})");
            _output.WriteLine($"  Calculated lat buffer: {latBuffer:F6} degrees");
            _output.WriteLine($"  Calculated lon buffer: {lonBuffer:F6} degrees");
            _output.WriteLine($"  Calculated buffer in km: {latBuffer * 111:F2} km (lat), {lonBuffer * 111:F2} km (lon)");
            
            // Test Case 4: California coordinates with different buffers
            _output.WriteLine("\n--- Test Case 4: California coordinates with various buffers ---");
            var californiaOrigin = new Coordinate { Lat = 32, Lon = -125 };
            var californiaDestination = new Coordinate { Lat = 42, Lon = -114 };
            
            for (double bufferKm = 0; bufferKm <= 50; bufferKm += 10)
            {
                var bbox = geometryUtils.ComputeBBox(californiaOrigin, californiaDestination, bufferKm);
                var geometryParam = $"{bbox.MinLon},{bbox.MinLat},{bbox.MaxLon},{bbox.MaxLat}";
                _output.WriteLine($"  California + {bufferKm}km buffer: {geometryParam}");
            }
            
            // Assertions
            Assert.True(true, "Coordinate transformation debug completed successfully");
            
            // Additional validation - ensure GeometryUtils produces valid bounding boxes
            var testBbox = geometryUtils.ComputeBBox(californiaOrigin, californiaDestination, 10);
            Assert.True(testBbox.MinLat <= testBbox.MaxLat, "MinLat should be <= MaxLat");
            Assert.True(testBbox.MinLon <= testBbox.MaxLon, "MinLon should be <= MaxLon");
            Assert.InRange(testBbox.MinLat, -90, 90);
            Assert.InRange(testBbox.MaxLat, -90, 90);
            Assert.InRange(testBbox.MinLon, -180, 180);
            Assert.InRange(testBbox.MaxLon, -180, 180);
            
            _output.WriteLine("\n✅ Coordinate transformation debug test completed!");
        }

        [Fact]
        public void TestBoundingBoxComputation()
        {
            _output.WriteLine("Bounding Box Computation Test");
            _output.WriteLine("============================");
            
            var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));
            var logger = loggerFactory.CreateLogger<GeometryUtils>();
            var geometryUtils = new GeometryUtils(logger);
            
            // Test with known coordinates
            var origin = new Coordinate { Lat = 34.0522, Lon = -118.2437 }; // Los Angeles
            var destination = new Coordinate { Lat = 34.1625, Lon = -118.1331 }; // Pasadena
            var buffer = 5.0; // 5km buffer
            
            var bbox = geometryUtils.ComputeBBox(origin, destination, buffer);
            
            // Validate the bounding box
            Assert.True(bbox.MinLat <= origin.Lat && origin.Lat <= bbox.MaxLat, "Origin latitude should be within bounding box");
            Assert.True(bbox.MinLat <= destination.Lat && destination.Lat <= bbox.MaxLat, "Destination latitude should be within bounding box");
            Assert.True(bbox.MinLon <= origin.Lon && origin.Lon <= bbox.MaxLon, "Origin longitude should be within bounding box");
            Assert.True(bbox.MinLon <= destination.Lon && destination.Lon <= bbox.MaxLon, "Destination longitude should be within bounding box");
            
            _output.WriteLine($"Origin: ({origin.Lat}, {origin.Lon})");
            _output.WriteLine($"Destination: ({destination.Lat}, {destination.Lon})");
            _output.WriteLine($"Buffer: {buffer}km");
            _output.WriteLine($"BBox: ({bbox.MinLon}, {bbox.MinLat}) to ({bbox.MaxLon}, {bbox.MaxLat})");
            _output.WriteLine("✅ Bounding box computation test passed");
        }
    }
}