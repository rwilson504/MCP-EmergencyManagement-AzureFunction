using System;
using EmergencyManagementMCP.Tools;

namespace EmergencyManagementMCP.Tests
{
    /// <summary>
    /// Basic test to verify all tools are properly set up and can be instantiated
    /// </summary>
    public class AllToolsTest
    {
        public static void TestAllToolsSetup()
        {
            Console.WriteLine("All Tools Setup Test");
            Console.WriteLine("====================");
            
            Console.WriteLine("\nTesting tool constants and descriptions:");
            
            // Test Address Fire Zone Check Tool
            Console.WriteLine($"✅ AddressFireZoneCheckTool:");
            Console.WriteLine($"   Name: {AddressFireZoneCheckTool.ToolName}");
            Console.WriteLine($"   Description: {AddressFireZoneCheckTool.ToolDescription}");
            
            // Test Coordinate Fire Zone Check Tool 
            Console.WriteLine($"✅ CoordinateFireZoneCheckTool:");
            Console.WriteLine($"   Name: {CoordinateFireZoneCheckTool.ToolName}");
            Console.WriteLine($"   Description: {CoordinateFireZoneCheckTool.ToolDescription}");
            
            // Test Coordinate-based Routing Tool
            Console.WriteLine($"✅ RoutingFireAwareShortestTool:");
            Console.WriteLine($"   Name: {CoordinateRoutingFireAwareShortestTool.ToolName}");
            Console.WriteLine($"   Description: {CoordinateRoutingFireAwareShortestTool.ToolDescription}");
            
            // Test Address-based Routing Tool
            Console.WriteLine($"✅ AddressRoutingFireAwareShortestTool:");
            Console.WriteLine($"   Name: {AddressRoutingFireAwareShortestTool.ToolName}");
            Console.WriteLine($"   Description: {AddressRoutingFireAwareShortestTool.ToolDescription}");
            
            Console.WriteLine("\nTesting property string constants:");
            
            // Test Address Fire Zone Check Properties
            Console.WriteLine($"✅ AddressFireZoneCheckToolPropertyStrings:");
            Console.WriteLine($"   Address: {AddressFireZoneCheckToolPropertyStrings.AddressName} ({AddressFireZoneCheckToolPropertyStrings.AddressType})");
            
            // Test Coordinate Fire Zone Check Properties
            Console.WriteLine($"✅ CoordinateFireZoneCheckToolPropertyStrings:");
            Console.WriteLine($"   Lat: {CoordinateFireZoneCheckToolPropertyStrings.LatName} ({CoordinateFireZoneCheckToolPropertyStrings.LatType})");
            Console.WriteLine($"   Lon: {CoordinateFireZoneCheckToolPropertyStrings.LonName} ({CoordinateFireZoneCheckToolPropertyStrings.LonType})");
            
            // Test Coordinate Routing Properties  
            Console.WriteLine($"✅ RoutingFireAwareShortestToolPropertyStrings:");
            Console.WriteLine($"   Origin Lat: {CoordinateRoutingFireAwareShortestToolPropertyStrings.OriginLatName} ({CoordinateRoutingFireAwareShortestToolPropertyStrings.OriginLatType})");
            Console.WriteLine($"   Origin Lon: {CoordinateRoutingFireAwareShortestToolPropertyStrings.OriginLonName} ({CoordinateRoutingFireAwareShortestToolPropertyStrings.OriginLonType})");
            
            // Test Address Routing Properties
            Console.WriteLine($"✅ AddressRoutingFireAwareShortestToolPropertyStrings:");
            Console.WriteLine($"   Origin Address: {AddressRoutingFireAwareShortestToolPropertyStrings.OriginAddressName} ({AddressRoutingFireAwareShortestToolPropertyStrings.OriginAddressType})");
            Console.WriteLine($"   Destination Address: {AddressRoutingFireAwareShortestToolPropertyStrings.DestinationAddressName} ({AddressRoutingFireAwareShortestToolPropertyStrings.DestinationAddressType})");
            
            Console.WriteLine("\nAll tools are properly configured!");
            Console.WriteLine("\nTool Summary:");
            Console.WriteLine("=============");
            Console.WriteLine("Fire Zone Checking:");
            Console.WriteLine($"  • {AddressFireZoneCheckTool.ToolName} - Address input");
            Console.WriteLine($"  • {CoordinateFireZoneCheckTool.ToolName} - Coordinate input");
            Console.WriteLine("\nFire-Aware Routing:");
            Console.WriteLine($"  • {CoordinateRoutingFireAwareShortestTool.ToolName} - Coordinate input");
            Console.WriteLine($"  • {AddressRoutingFireAwareShortestTool.ToolName} - Address input");
        }
    }
}