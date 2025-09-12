using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;
using Xunit.Abstractions;
using EmergencyManagementMCP.Models;

namespace EmergencyManagementMcp.IntegrationTests
{
    /// <summary>
    /// Test to reproduce the exact empty features issue from GitHub issue #59
    /// </summary>
    public class EmptyFeaturesIssueTest
    {
        private readonly ITestOutputHelper _output;

        public EmptyFeaturesIssueTest(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void TestWhenEmptyRouteSpecIsCreated()
        {
            _output.WriteLine("=== Testing Empty RouteSpec Scenario ===");
            
            // This is the problematic JSON from the GitHub issue
            var problematicJson = @"{
                ""type"": ""FeatureCollection"",
                ""features"": [],
                ""avoidAreas"": null,
                ""routeOutputOptions"": [""routePath""],
                ""travelMode"": ""driving""
            }";

            _output.WriteLine("Original problematic JSON:");
            _output.WriteLine(problematicJson);

            // Test if this could be created by default constructor
            var defaultRouteSpec = new RouteSpec();
            var defaultJson = JsonSerializer.Serialize(defaultRouteSpec, new JsonSerializerOptions 
            { 
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            });
            
            _output.WriteLine("");
            _output.WriteLine("Default RouteSpec JSON:");
            _output.WriteLine(defaultJson);

            // Test if this could be created by default AzureMapsPostData
            var defaultAzureMaps = new AzureMapsPostData();
            var defaultAzureMapsJson = JsonSerializer.Serialize(defaultAzureMaps, new JsonSerializerOptions 
            { 
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            });
            
            _output.WriteLine("");
            _output.WriteLine("Default AzureMapsPostData JSON:");
            _output.WriteLine(defaultAzureMapsJson);

            // Verify defaults
            Assert.Equal("FeatureCollection", defaultRouteSpec.Type);
            Assert.Empty(defaultRouteSpec.Features); // This matches the issue!
            Assert.Null(defaultRouteSpec.AvoidAreas); // This matches the issue!
            Assert.Equal(new[] { "routePath" }, defaultRouteSpec.RouteOutputOptions); // This matches the issue!
            Assert.Equal("driving", defaultRouteSpec.TravelMode); // This matches the issue!

            _output.WriteLine("");
            _output.WriteLine("✅ Found the source of empty data:");
            _output.WriteLine("  - Default RouteSpec constructor creates empty features array");
            _output.WriteLine("  - Default RouteSpec has null avoidAreas");
            _output.WriteLine("  - This matches exactly the problematic JSON from the issue");
            
            // Now let's see where this default could be coming from
            _output.WriteLine("");
            _output.WriteLine("Potential sources of default RouteSpec:");
            _output.WriteLine("  1. JsonSerializer.Deserialize<AzureMapsPostData> returning default when JSON is invalid");
            _output.WriteLine("  2. Error handling creating a default RouteSpec");
            _output.WriteLine("  3. Empty/null azureMapsPostJson being passed to RouteLinkService");
        }

        [Fact]
        public void TestJsonDeserializationFailure()
        {
            _output.WriteLine("=== Testing JSON Deserialization Edge Cases ===");
            
            // Test what happens with empty JSON
            try
            {
                var emptyResult = JsonSerializer.Deserialize<AzureMapsPostData>("");
                _output.WriteLine($"Empty string deserialization: {(emptyResult == null ? "null" : "not null")}");
            }
            catch (Exception ex)
            {
                _output.WriteLine($"Empty string deserialization throws: {ex.GetType().Name}");
            }

            // Test what happens with null JSON  
            try
            {
                var nullResult = JsonSerializer.Deserialize<AzureMapsPostData>((string)null!);
                _output.WriteLine($"Null string deserialization: {(nullResult == null ? "null" : "not null")}");
            }
            catch (Exception ex)
            {
                _output.WriteLine($"Null string deserialization throws: {ex.GetType().Name}");
            }

            // Test what happens with invalid JSON
            try
            {
                var invalidResult = JsonSerializer.Deserialize<AzureMapsPostData>("invalid json");
                _output.WriteLine($"Invalid JSON deserialization: {(invalidResult == null ? "null" : "not null")}");
            }
            catch (Exception ex)
            {
                _output.WriteLine($"Invalid JSON deserialization throws: {ex.GetType().Name}");
            }

            // Test what happens with incomplete JSON
            var incompleteJson = @"{""type"": ""FeatureCollection""}";
            var incompleteResult = JsonSerializer.Deserialize<AzureMapsPostData>(incompleteJson);
            Assert.NotNull(incompleteResult);
            Assert.Equal("FeatureCollection", incompleteResult.Type);
            Assert.Empty(incompleteResult.Features); // Default empty array
            
            var incompleteResultJson = JsonSerializer.Serialize(incompleteResult, new JsonSerializerOptions 
            { 
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            });
            
            _output.WriteLine("");
            _output.WriteLine("Incomplete JSON result:");
            _output.WriteLine(incompleteResultJson);
            
            _output.WriteLine("");
            _output.WriteLine("✅ Incomplete JSON deserialization also creates empty features!");
        }
    }
}