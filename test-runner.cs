using EmergencyManagementMCP.Models;
using EmergencyManagementMCP.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;

// Simple console test runner
Console.WriteLine("Starting California Bounding Box Test...");

try
{
    EmergencyManagementMCP.Tests.CaliforniaBoundingBoxTest.TestCaliforniaCoordinates();
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
}

Console.WriteLine("Test completed.");