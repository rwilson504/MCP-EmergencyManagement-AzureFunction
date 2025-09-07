# Logical Flow Diagram

This diagram shows the logical flow of the fire-aware routing process in simple terms for non-technical users.

```mermaid
flowchart TD
    %% User Journey
    Start([👤 User asks AI Agent:<br/>"Find safe route from A to B<br/>avoiding wildfires"])
    
    %% Step 1: Initial Processing
    Agent[🤖 AI Agent calls<br/>Fire-Aware Routing Tool]
    
    %% Step 2: Input Validation
    Validate{✅ Valid coordinates<br/>and parameters?}
    
    %% Step 3: Area Analysis
    BoundingBox[📏 Calculate search area<br/>around your route<br/>+ safety buffer]
    
    %% Step 4: Fire Data
    CheckCache{💾 Recent fire data<br/>already available?}
    FetchFires[🔥 Get latest wildfire<br/>information from<br/>government databases]
    UseCache[📋 Use cached<br/>fire data<br/>< 10 minutes old]
    
    %% Step 5: Avoid Areas
    BuildAvoids[🚫 Create areas to avoid<br/>around active fires<br/>+ road closures]
    
    %% Step 6: Route Calculation
    CallMaps[🗺️ Calculate safest route<br/>using Azure Maps<br/>avoiding danger zones]
    
    %% Step 7: Response
    BuildResponse[📍 Prepare route response<br/>with turn-by-turn directions<br/>and fire zone info]
    
    %% Final Result
    Success[✅ Return safe route with:<br/>• GPS coordinates<br/>• Driving directions<br/>• Distance & time<br/>• Areas avoided]
    
    Error[❌ Return error message<br/>with explanation]
    
    %% Flow Connections
    Start --> Agent
    Agent --> Validate
    
    Validate -->|Yes| BoundingBox
    Validate -->|No| Error
    
    BoundingBox --> CheckCache
    
    CheckCache -->|Yes| UseCache
    CheckCache -->|No| FetchFires
    
    UseCache --> BuildAvoids
    FetchFires --> BuildAvoids
    
    BuildAvoids --> CallMaps
    CallMaps --> BuildResponse
    BuildResponse --> Success
    
    %% Styling for clarity
    classDef userAction fill:#e8f5e8,stroke:#4caf50,stroke-width:2px
    classDef processing fill:#e3f2fd,stroke:#2196f3,stroke-width:2px
    classDef dataSource fill:#fff3e0,stroke:#ff9800,stroke-width:2px
    classDef decision fill:#f3e5f5,stroke:#9c27b0,stroke-width:2px
    classDef result fill:#e8f5e8,stroke:#4caf50,stroke-width:3px
    classDef error fill:#ffebee,stroke:#f44336,stroke-width:2px
    
    class Start,Agent userAction
    class BoundingBox,BuildAvoids,CallMaps,BuildResponse processing
    class FetchFires,UseCache dataSource
    class Validate,CheckCache decision
    class Success result
    class Error error
```

## What Happens Step by Step:

### 🚀 **User Request**
A user asks their AI assistant: *"Find me a safe route from Los Angeles to San Francisco that avoids any active wildfires"*

### 🤖 **AI Agent Processing**
The AI agent understands this requires fire-aware routing and calls the Emergency Management MCP tool with the coordinates.

### ✅ **Input Validation**
The system checks that:
- Coordinates are valid (latitude/longitude within proper ranges)
- Route distance is reasonable
- Safety buffer distance makes sense

### 📏 **Search Area Calculation**
The system calculates a bounding box around your intended route plus a safety buffer to search for nearby fires.

### 🔥 **Fire Data Retrieval**
- **If recent data exists**: Uses cached wildfire information (updated within last 10 minutes)
- **If data is stale**: Fetches the latest wildfire perimeter data from government sources (ArcGIS/NIFC)

### 🚫 **Danger Zone Mapping**
Creates "avoid areas" around:
- Active wildfire perimeters (with safety buffer)
- Road closures due to emergencies
- Other hazardous zones

### 🗺️ **Smart Route Calculation**
Uses Azure Maps routing engine to calculate the optimal route while:
- Avoiding all marked danger zones
- Minimizing travel time and distance
- Providing realistic driving directions

### 📍 **Results Delivered**
Returns a comprehensive route package including:
- **GPS Route**: Exact coordinates for navigation apps
- **Turn-by-Turn Directions**: Human-readable driving instructions  
- **Route Statistics**: Total distance, estimated travel time
- **Safety Information**: Which fire zones were avoided
- **Visual Map**: GeoJSON data for map display

## Why This Architecture is Powerful:

### 🌐 **Multiple Data Sources**
Combines real-time wildfire data from government sources with commercial mapping services for the most accurate and safe routing.

### ⚡ **Performance Optimized**
- Caches fire data to avoid repeated API calls
- Uses Azure's global infrastructure for fast response times
- Processes only the geographic area relevant to your route

### 🔒 **Enterprise Security**
- No API keys stored in code
- Secure Azure identity management
- Comprehensive logging for audit trails

### 🎯 **Purpose-Built for Emergencies**
Unlike general mapping apps, this tool is specifically designed for emergency scenarios where avoiding danger zones is critical for safety.