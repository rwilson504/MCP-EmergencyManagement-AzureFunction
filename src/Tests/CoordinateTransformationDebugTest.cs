using EmergencyManagementMCP.Models;
using EmergencyManagementMCP.Services;
using Microsoft.Extensions.Logging;

namespace EmergencyManagementMCP.Tests
{
    /// <summary>
    /// Test to investigate the coordinate transformation issue mentioned in the GitHub issue.
    /// The user reported that California coordinates (-125,32,-114,42) were transformed to
    /// unexpected values (29.98198198198198,32.98198198198198,33.01801801801802,33.01801801801802)
    /// </summary>
    public class CoordinateTransformationDebugTest
    {
        public static void TestCoordinateTransformation()
        {
            Console.WriteLine("Coordinate Transformation Debug Test");
            Console.WriteLine("=====================================");
            
            // Create a mock logger for testing
            var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
            var logger = loggerFactory.CreateLogger<GeometryUtils>();
            
            var geometryUtils = new GeometryUtils(logger);
            
            Console.WriteLine("Testing various scenarios that might produce the unexpected coordinates...");
            
            // Test Case 1: Small area around (32, 33) with different buffer sizes
            Console.WriteLine("\n--- Test Case 1: Small area with buffers ---");
            var origin1 = new Coordinate { Lat = 32, Lon = 32 };
            var destination1 = new Coordinate { Lat = 33, Lon = 33 };
            
            for (double bufferKm = 0; bufferKm <= 100; bufferKm += 10)
            {
                var bbox1 = geometryUtils.ComputeBBox(origin1, destination1, bufferKm);
                var geometryParam1 = $"{bbox1.MinLon},{bbox1.MinLat},{bbox1.MaxLon},{bbox1.MaxLat}";
                Console.WriteLine($"  Buffer {bufferKm}km: {geometryParam1}");
                
                // Check if this matches the unexpected coordinates
                if (Math.Abs(bbox1.MinLon - 29.98198198198198) < 0.001)
                {
                    Console.WriteLine($"    ⚠️  MATCH FOUND! Buffer {bufferKm}km produces similar coordinates");
                }
            }
            
            // Test Case 2: Different coordinate pairs that might produce the unexpected result
            Console.WriteLine("\n--- Test Case 2: Testing coordinate pairs around 32-33 range ---");
            var testCoords = new[]
            {
                (32.0, 33.0, 0.0),   // No buffer
                (32.5, 33.5, 1.0),  // Small buffer
                (32.1, 33.1, 2.0),  // Different buffer
            };
            
            foreach (var (lat, lon, buffer) in testCoords)
            {
                var origin = new Coordinate { Lat = lat, Lon = lon };
                var destination = new Coordinate { Lat = lat + 0.1, Lon = lon + 0.1 };
                var bbox = geometryUtils.ComputeBBox(origin, destination, buffer);
                var geometryParam = $"{bbox.MinLon},{bbox.MinLat},{bbox.MaxLon},{bbox.MaxLat}";
                Console.WriteLine($"  Coords ({lat},{lon}) + {buffer}km buffer: {geometryParam}");
            }
            
            // Test Case 3: Reverse engineer the buffer calculation
            Console.WriteLine("\n--- Test Case 3: Reverse engineering the buffer calculation ---");
            
            // From the issue: 29.98198198198198,32.98198198198198,33.01801801801802,33.01801801801802
            // Let's see what buffer would produce this from coordinates around (32,33)
            double unexpectedMinLon = 29.98198198198198;
            double unexpectedMinLat = 32.98198198198198;
            double unexpectedMaxLon = 33.01801801801802;
            double unexpectedMaxLat = 33.01801801801802;
            
            // If the center is around (32,33), calculate what buffer was applied
            double centerLat = (unexpectedMinLat + unexpectedMaxLat) / 2;
            double centerLon = (unexpectedMinLon + unexpectedMaxLon) / 2;
            double latBuffer = (unexpectedMaxLat - unexpectedMinLat) / 2;
            double lonBuffer = (unexpectedMaxLon - unexpectedMinLon) / 2;
            
            Console.WriteLine($"  Unexpected coordinates center: ({centerLon:F6}, {centerLat:F6})");
            Console.WriteLine($"  Calculated lat buffer: {latBuffer:F6} degrees");
            Console.WriteLine($"  Calculated lon buffer: {lonBuffer:F6} degrees");
            Console.WriteLine($"  Calculated buffer in km: {latBuffer * 111:F2} km (lat), {lonBuffer * 111:F2} km (lon)");
            
            // Test Case 4: California coordinates with different buffers
            Console.WriteLine("\n--- Test Case 4: California coordinates with buffers ---");
            var californiaOrigin = new Coordinate { Lat = 32, Lon = -125 };
            var californiaDestination = new Coordinate { Lat = 42, Lon = -114 };
            
            for (double bufferKm = 0; bufferKm <= 50; bufferKm += 10)
            {
                var californiaBbox = geometryUtils.ComputeBBox(californiaOrigin, californiaDestination, bufferKm);
                var californiaGeometryParam = $"{californiaBbox.MinLon},{californiaBbox.MinLat},{californiaBbox.MaxLon},{californiaBbox.MaxLat}";
                Console.WriteLine($"  California + {bufferKm}km buffer: {californiaGeometryParam}");
            }
            
            Console.WriteLine("\n✅ Coordinate transformation debug test completed!");
            Console.WriteLine("\nAnalysis: The unexpected coordinates seem to be from a small geographic area");
            Console.WriteLine("with a buffer applied, not from California coordinates. This suggests the issue");
            Console.WriteLine("might be with how the bounding box is computed for a specific route or area.");
        }
    }
}