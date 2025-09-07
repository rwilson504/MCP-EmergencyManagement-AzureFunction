# Enhanced Logging Test Documentation

This document demonstrates the enhanced logging features added to the Emergency Management MCP Function App.

## Testing the Enhanced Logging

### 1. Local Development Testing

To test the enhanced logging locally:

```bash
# Navigate to the source directory
cd src

# Copy the default settings
cp local.settings.json.default local.settings.json

# Edit local.settings.json with your Azure credentials
# Set appropriate log levels for debugging

# Start the function app
func start
```

### 2. Expected Log Output Examples

#### Successful Route Request
```
[2025-01-09 10:30:45] Information: Emergency Management MCP Function App starting up...
[2025-01-09 10:30:45] Information: GeoServiceClient initialized with ArcGIS URL: https://services3.arcgis.com/...
[2025-01-09 10:30:45] Information: RouterClient initialized with Maps API base: https://atlas.microsoft.com
[2025-01-09 10:30:45] Information: GeoJsonCache initialized successfully: container=routing-cache
[2025-01-09 10:30:45] Information: Emergency Management MCP Function App started successfully

[2025-01-09 10:31:00] Information: Starting fire-aware routing request: origin=(37.7749,-122.4194), destination=(37.8044,-122.2711), bufferKm=2, useClosures=true, traceId=a1b2c3d4
[2025-01-09 10:31:00] Debug: Step 1: Computing bounding box, traceId=a1b2c3d4
[2025-01-09 10:31:00] Debug: Step 1 completed in 2ms: bbox computed, traceId=a1b2c3d4
[2025-01-09 10:31:00] Debug: Step 3: Loading fire perimeter data, traceId=a1b2c3d4
[2025-01-09 10:31:00] Information: Cache miss: key=fire-perimeters-37.772-122.422-37.807-122.268, requestId=e5f6g7h8
[2025-01-09 10:31:00] Debug: Making ArcGIS request: https://services3.arcgis.com/..., requestId=e5f6g7h8
[2025-01-09 10:31:01] Information: Successfully fetched fire perimeter GeoJSON: length=15420 chars, elapsed=1250ms, requestId=e5f6g7h8
[2025-01-09 10:31:01] Debug: Step 3 completed in 1255ms: fire data loaded, size=15420 chars, traceId=a1b2c3d4
[2025-01-09 10:31:01] Debug: Step 4: Building avoid rectangles from fire data, traceId=a1b2c3d4
[2025-01-09 10:31:01] Information: Built 3 avoid rectangles from GeoJSON with 2km buffer (processed 5 features), requestId=i9j0k1l2
[2025-01-09 10:31:01] Debug: Step 4 completed in 15ms: 3 avoid rectangles built, traceId=a1b2c3d4
[2025-01-09 10:31:01] Debug: Step 7: Calculating route with 3 avoid areas, traceId=a1b2c3d4
[2025-01-09 10:31:01] Debug: Added 3 avoid areas (of 3 requested), requestId=m3n4o5p6
[2025-01-09 10:31:02] Information: Route calculated successfully: distance=15420m, time=1320s, elapsed=850ms, requestId=m3n4o5p6
[2025-01-09 10:31:02] Debug: Step 7 completed in 855ms: route calculated, traceId=a1b2c3d4
[2025-01-09 10:31:02] Information: Fire-aware route calculated successfully: distance=15420m, time=1320s, avoids=3, totalTime=2127ms, traceId=a1b2c3d4
```

#### Error Scenarios

**Invalid Coordinates:**
```
[2025-01-09 10:32:00] Information: Starting fire-aware routing request: origin=(91.0,-200.0), destination=(37.8044,-122.2711), bufferKm=2, useClosures=true, traceId=q7r8s9t0
[2025-01-09 10:32:00] Error: Invalid origin coordinates: lat=91, lon=-200, traceId=q7r8s9t0
```

**Azure Maps API Error:**
```
[2025-01-09 10:33:00] Debug: Making Azure Maps API request, requestId=u1v2w3x4
[2025-01-09 10:33:01] Error: Azure Maps API returned error 401: Unauthorized - Invalid subscription key, requestId=u1v2w3x4
[2025-01-09 10:33:01] Error: HTTP error calling Azure Maps routing API after 1150ms, requestId=u1v2w3x4
```

**Storage Cache Error:**
```
[2025-01-09 10:34:00] Debug: Checking cache existence for blob: fire-perimeters-37.772-122.422-37.807-122.268.json, requestId=y5z6a7b8
[2025-01-09 10:34:01] Error: Azure storage error in cache operation: key=fire-perimeters-37.772-122.422-37.807-122.268, statusCode=403, elapsed=1050ms, requestId=y5z6a7b8
[2025-01-09 10:34:01] Warning: Falling back to refresher due to cache error, requestId=y5z6a7b8
[2025-01-09 10:34:02] Information: Successfully fetched fire perimeter GeoJSON: length=15420 chars, elapsed=1200ms, requestId=c9d0e1f2
```

### 3. Log Correlation Features

Each request generates multiple related log entries that can be traced using:

- **traceId**: Unique identifier for each MCP tool invocation (e.g., `a1b2c3d4`)
- **requestId**: Unique identifier for each service call (e.g., `e5f6g7h8`)

Example correlation:
```bash
# Find all logs for a specific route calculation
grep "traceId=a1b2c3d4" function.log

# Find all Azure Maps API calls
grep "Azure Maps" function.log

# Find all cache operations
grep "cache operation" function.log
```

### 4. Performance Monitoring

The enhanced logging includes timing information for all major operations:

- **Total request time**: Complete route calculation duration
- **Service call times**: Individual API call durations
- **Cache operation times**: Storage read/write performance
- **Processing step times**: Each step in the route calculation

### 5. Debugging Common Issues

#### Configuration Issues
```
[Error] Storage:BlobServiceUrl configuration is missing
[Error] GeoServiceClient initialization failed: Invalid URI
[Error] Maps:Key configuration is required
```

#### Network/Timeout Issues
```
[Error] Timeout calling Azure Maps routing API after 30000ms, requestId=xyz
[Error] Timeout fetching fire perimeters from ArcGIS after 30000ms, requestId=abc
```

#### JSON Parsing Issues
```
[Error] Failed to parse Azure Maps JSON response, requestId=def
[Error] Failed to parse GeoJSON: Unexpected character at position 15, requestId=ghi
```

## Benefits of Enhanced Logging

1. **Faster Issue Resolution**: Detailed error context and correlation IDs
2. **Performance Optimization**: Timing data to identify bottlenecks
3. **Security**: Sensitive data excluded from logs
4. **Production Monitoring**: Application Insights integration
5. **Debugging Support**: Step-by-step execution tracing

## Next Steps

- Test with real Azure Maps keys and storage accounts
- Monitor Application Insights dashboards in production
- Set up alerting for error thresholds
- Create custom Log Analytics queries for specific scenarios