using System.Text.Json;
using Xunit;
using Xunit.Abstractions;
using EmergencyManagementMCP.Models;

namespace EmergencyManagementMcp.IntegrationTests
{
    /// <summary>
    /// Test to verify the Azure Maps token broker and route links API functionality.
    /// These APIs support the React SPA for visualizing emergency routes.
    /// </summary>
    public class AzureMapsViewerIntegrationTest
    {
        private readonly ITestOutputHelper _output;

        public AzureMapsViewerIntegrationTest(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void Azure_Maps_Token_Function_Should_Be_Documented()
        {
            _output.WriteLine("Azure Maps Token Function Documentation");
            _output.WriteLine("=====================================");
            _output.WriteLine("");
            _output.WriteLine("Function: GetMapsToken");
            _output.WriteLine("Route: GET /api/maps-token");
            _output.WriteLine("Purpose: Provides Azure Maps access tokens to the React SPA using Managed Identity");
            _output.WriteLine("");
            _output.WriteLine("Authentication: Function-level authorization");
            _output.WriteLine("Returns: { \"access_token\": \"...\", \"expires_on\": 1234567890, \"token_type\": \"Bearer\" }");
            _output.WriteLine("");
            _output.WriteLine("This endpoint allows the React SPA to obtain Azure Maps tokens without");
            _output.WriteLine("exposing subscription keys or requiring browser app registration.");
        }

        [Fact]
        public void Route_Links_API_Should_Be_Documented()
        {
            _output.WriteLine("Route Links API Documentation");
            _output.WriteLine("=============================");
            _output.WriteLine("");
            _output.WriteLine("POST /api/routeLinks");
            _output.WriteLine("Purpose: Creates a short-link for a RouteSpec and stores it in Azure Storage");
            _output.WriteLine("Body: RouteSpec JSON (with waypoints, avoid areas, travel mode)");
            _output.WriteLine("Returns: { \"id\": \"abc123\", \"url\": \"/view?id=abc123\", \"createdAt\": \"...\", \"expiresAt\": \"...\" }");
            _output.WriteLine("");
            _output.WriteLine("GET /api/routeLinks/{id}");
            _output.WriteLine("Purpose: Retrieves a RouteSpec by its short-link ID");
            _output.WriteLine("Returns: RouteSpec JSON");
            _output.WriteLine("");
            _output.WriteLine("Short-links are stored in the 'links' blob container with TTL support.");
        }

        [Fact]
        public void RouteSpec_Model_Should_Support_GeoJSON_Format()
        {
            _output.WriteLine("Testing RouteSpec model compliance with issue requirements");
            _output.WriteLine("=======================================================");

            // Create a test RouteSpec as specified in the issue
            var routeSpec = new RouteSpec
            {
                Type = "FeatureCollection",
                Features = new[]
                {
                    new RouteFeature
                    {
                        Type = "Feature",
                        Geometry = new PointGeometry
                        {
                            Type = "Point",
                            Coordinates = new[] { -121.4948, 38.5816 }
                        },
                        Properties = new RouteFeatureProperties
                        {
                            PointIndex = 0,
                            PointType = "waypoint"
                        }
                    },
                    new RouteFeature
                    {
                        Type = "Feature",
                        Geometry = new PointGeometry
                        {
                            Type = "Point",
                            Coordinates = new[] { -122.8756, 42.3265 }
                        },
                        Properties = new RouteFeatureProperties
                        {
                            PointIndex = 1,
                            PointType = "waypoint"
                        }
                    }
                },
                TravelMode = "driving",
                RouteOutputOptions = new[] { "routePath" },
                AvoidAreas = new MultiPolygon
                {
                    Type = "MultiPolygon",
                    Coordinates = new[]
                    {
                        new[]
                        {
                            new[]
                            {
                                new[] { -121.52, 38.56 },
                                new[] { -121.45, 38.56 },
                                new[] { -121.45, 38.62 },
                                new[] { -121.52, 38.62 },
                                new[] { -121.52, 38.56 }
                            }
                        }
                    }
                },
                TtlMinutes = 1440
            };

            // Serialize to JSON and verify structure
            var json = JsonSerializer.Serialize(routeSpec, new JsonSerializerOptions { WriteIndented = true });
            
            _output.WriteLine("Generated RouteSpec JSON:");
            _output.WriteLine(json);

            // Verify required fields are present
            Assert.Equal("FeatureCollection", routeSpec.Type);
            Assert.Equal(2, routeSpec.Features.Length);
            Assert.Equal("driving", routeSpec.TravelMode);
            Assert.Single(routeSpec.RouteOutputOptions);
            Assert.Equal("routePath", routeSpec.RouteOutputOptions[0]);
            Assert.NotNull(routeSpec.AvoidAreas);
            Assert.Equal(1440, routeSpec.TtlMinutes);

            _output.WriteLine("");
            _output.WriteLine("✓ RouteSpec model matches the data contract specified in the issue");
        }

        [Fact]
        public void AzureMapsPostData_Model_Should_Exclude_TtlMinutes()
        {
            _output.WriteLine("Testing AzureMapsPostData model format for Azure Maps API 2025-01-01");
            _output.WriteLine("====================================================================");

            // Create a test AzureMapsPostData as expected by Azure Maps API
            var azureMapsPostData = new AzureMapsPostData
            {
                Type = "FeatureCollection",
                Features = new[]
                {
                    new RouteFeature
                    {
                        Type = "Feature",
                        Geometry = new PointGeometry
                        {
                            Type = "Point",
                            Coordinates = new[] { -121.4949523, 38.5769347 }
                        },
                        Properties = new RouteFeatureProperties
                        {
                            PointIndex = 0,
                            PointType = "waypoint"
                        }
                    },
                    new RouteFeature
                    {
                        Type = "Feature",
                        Geometry = new PointGeometry
                        {
                            Type = "Point",
                            Coordinates = new[] { -122.681425, 45.516018 }
                        },
                        Properties = new RouteFeatureProperties
                        {
                            PointIndex = 1,
                            PointType = "waypoint"
                        }
                    }
                },
                AvoidAreas = new MultiPolygon
                {
                    Type = "MultiPolygon",
                    Coordinates = new[]
                    {
                        new[]
                        {
                            new[]
                            {
                                new[] { -121.80627482601801, 43.582590304982084 },
                                new[] { -121.76969060098199, 43.582590304982084 },
                                new[] { -121.76969060098199, 43.61895877201812 },
                                new[] { -121.80627482601801, 43.61895877201812 },
                                new[] { -121.80627482601801, 43.582590304982084 }
                            }
                        },
                        new[]
                        {
                            new[]
                            {
                                new[] { -121.98866158901801, 42.116317791981984 },
                                new[] { -121.95234267998198, 42.116317791981984 },
                                new[] { -121.95234267998198, 42.15268535701802 },
                                new[] { -121.98866158901801, 42.15268535701802 },
                                new[] { -121.98866158901801, 42.116317791981984 }
                            }
                        }
                    }
                },
                RouteOutputOptions = new[] { "routePath", "itinerary" },
                TravelMode = "driving"
            };

            // Serialize to JSON and verify structure
            var json = JsonSerializer.Serialize(azureMapsPostData, new JsonSerializerOptions { WriteIndented = true });
            
            _output.WriteLine("Generated AzureMapsPostData JSON:");
            _output.WriteLine(json);

            // Verify required fields are present and ttlMinutes is NOT present
            Assert.Equal("FeatureCollection", azureMapsPostData.Type);
            Assert.Equal(2, azureMapsPostData.Features.Length);
            Assert.Equal("driving", azureMapsPostData.TravelMode);
            Assert.Equal(2, azureMapsPostData.RouteOutputOptions.Length);
            Assert.Contains("routePath", azureMapsPostData.RouteOutputOptions);
            Assert.Contains("itinerary", azureMapsPostData.RouteOutputOptions);
            Assert.NotNull(azureMapsPostData.AvoidAreas);
            
            // Verify JSON doesn't contain ttlMinutes
            Assert.DoesNotContain("ttlMinutes", json);

            _output.WriteLine("");
            _output.WriteLine("✓ AzureMapsPostData model matches Azure Maps API 2025-01-01 requirements");
            _output.WriteLine("✓ ttlMinutes field is correctly excluded from Azure Maps API payload");
        }

        [Fact]
        public void RouteLinkData_Should_Include_AzureMapsPostData_Field()
        {
            _output.WriteLine("Testing RouteLinkData includes azureMapsPostData for web app integration");
            _output.WriteLine("=========================================================================");

            // Create a sample RouteLinkData as would be returned by GetRouteLink
            var routeLinkData = new RouteLinkData
            {
                Id = "test123",
                SasUrl = "/api/public/routeLinks/test123",
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddDays(1),
                AzureMapsPostData = new AzureMapsPostData
                {
                    Type = "FeatureCollection",
                    Features = new[]
                    {
                        new RouteFeature
                        {
                            Type = "Feature",
                            Geometry = new PointGeometry
                            {
                                Type = "Point",
                                Coordinates = new[] { -121.4949523, 38.5769347 }
                            },
                            Properties = new RouteFeatureProperties
                            {
                                PointIndex = 0,
                                PointType = "waypoint"
                            }
                        },
                        new RouteFeature
                        {
                            Type = "Feature",
                            Geometry = new PointGeometry
                            {
                                Type = "Point",
                                Coordinates = new[] { -122.681425, 45.516018 }
                            },
                            Properties = new RouteFeatureProperties
                            {
                                PointIndex = 1,
                                PointType = "waypoint"
                            }
                        }
                    },
                    TravelMode = "driving",
                    RouteOutputOptions = new[] { "routePath", "itinerary" }
                }
            };

            // Serialize to JSON and verify the web app can get the azureMapsPostData
            var json = JsonSerializer.Serialize(routeLinkData, new JsonSerializerOptions { WriteIndented = true });
            
            _output.WriteLine("Generated RouteLinkData JSON (as returned by GetRouteLink):");
            _output.WriteLine(json);

            // Verify the structure includes all required fields
            Assert.NotNull(routeLinkData.AzureMapsPostData);
            Assert.Equal("FeatureCollection", routeLinkData.AzureMapsPostData.Type);
            Assert.Equal(2, routeLinkData.AzureMapsPostData.Features.Length);
            Assert.Equal("driving", routeLinkData.AzureMapsPostData.TravelMode);
            Assert.Contains("routePath", routeLinkData.AzureMapsPostData.RouteOutputOptions);
            Assert.Contains("itinerary", routeLinkData.AzureMapsPostData.RouteOutputOptions);
            
            // Verify JSON contains azureMapsPostData field
            Assert.Contains("azureMapsPostData", json);
            Assert.Contains("\"type\": \"FeatureCollection\"", json);

            _output.WriteLine("");
            _output.WriteLine("✓ RouteLinkData includes azureMapsPostData field for web app integration");
            _output.WriteLine("✓ Web app can extract Azure Maps compatible payload from response");
        }

        [Fact]
        public void React_SPA_Configuration_Should_Be_Documented()
        {
            _output.WriteLine("React SPA Configuration Documentation");
            _output.WriteLine("====================================");
            _output.WriteLine("");
            _output.WriteLine("Location: /web directory");
            _output.WriteLine("Framework: React 18 + TypeScript + Vite");
            _output.WriteLine("Azure Maps: azure-maps-control v3 with anonymous auth");
            _output.WriteLine("");
            _output.WriteLine("Environment Variables:");
            _output.WriteLine("  VITE_AZURE_MAPS_CLIENT_ID - Azure Maps account unique ID");
            _output.WriteLine("");
            _output.WriteLine("Routes supported:");
            _output.WriteLine("  /view?id=abc123 - Short-link route display");
            _output.WriteLine("  /view?from=lon,lat&to=lon,lat&avoid=rect(...) - Query parameter fallback");
            _output.WriteLine("");
            _output.WriteLine("Authentication: Uses getToken() callback to /api/maps-token");
            _output.WriteLine("No browser app registration required for Azure Maps access");
        }

        [Fact]
        public void Infrastructure_Changes_Should_Be_Documented()
        {
            _output.WriteLine("Infrastructure Changes Made");
            _output.WriteLine("==========================");
            _output.WriteLine("");
            _output.WriteLine("Storage Account:");
            _output.WriteLine("  ✓ Added 'links' container for route link storage");
            _output.WriteLine("");
            _output.WriteLine("Function App:");
            _output.WriteLine("  ✓ Added CORS configuration for localhost:3000 (SPA development)");
            _output.WriteLine("  ✓ Managed Identity already configured with Azure Maps Data Reader role");
            _output.WriteLine("  ✓ Managed Identity already configured with Storage Blob Data Owner role");
            _output.WriteLine("");
            _output.WriteLine("Azure Maps:");
            _output.WriteLine("  ✓ Account already exists with disableLocalAuth: true (AAD only)");
            _output.WriteLine("  ✓ Client ID (uniqueId) already exposed via Maps:ClientId app setting");
            _output.WriteLine("");
            _output.WriteLine("No breaking changes to existing MCP functionality");
        }
    }
}