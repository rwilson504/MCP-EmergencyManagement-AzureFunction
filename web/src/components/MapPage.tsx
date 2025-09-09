import { useEffect, useRef, useState } from 'react';
import * as atlas from 'azure-maps-control';
import 'azure-maps-control/dist/atlas.min.css';

interface RouteSpec {
  type: 'FeatureCollection';
  features: Array<{
    type: 'Feature';
    geometry: {
      type: 'Point';
      coordinates: [number, number];
    };
    properties: {
      pointIndex: number;
      pointType: 'waypoint';
    };
  }>;
  travelMode: 'driving';
  routeOutputOptions: ['routePath'];
  avoidAreas?: {
    type: 'MultiPolygon';
    coordinates: number[][][][];
  };
  ttlMinutes?: number;
}

export default function MapPage() {
  const mapRef = useRef<HTMLDivElement>(null);
  const mapInstanceRef = useRef<atlas.Map | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const rectToRing = (s: string): number[][][] => {
    const values = s.match(/-?\d+(\.\d+)?/g);
    if (!values || values.length < 4) {
      throw new Error('Invalid rectangle format');
    }
    const [xmin, ymin, xmax, ymax] = values.map(Number);
    return [[[xmin, ymin], [xmax, ymin], [xmax, ymax], [xmin, ymax], [xmin, ymin]]];
  };

  const getRouteSpec = async (): Promise<RouteSpec> => {
    const qp = new URLSearchParams(location.search);
    const id = qp.get('id');
    
    // Get API base URL from environment
    const apiBaseUrl = import.meta.env.VITE_API_BASE_URL || '/api';
    
    if (id) {
      // Short-link flow: fetch from API
      const response = await fetch(`${apiBaseUrl}/routeLinks/${encodeURIComponent(id)}`, { 
        credentials: 'include' 
      });
      if (!response.ok) {
        throw new Error(`Failed to fetch route: ${response.status} ${response.statusText}`);
      }
      return await response.json();
    }
    
    // Query fallback: build from URL parameters
    const fromStr = qp.get('from');
    const toStr = qp.get('to');
    const avoidStr = qp.get('avoid') || '';
    
    if (!fromStr || !toStr) {
      throw new Error('Missing required parameters: from and to coordinates');
    }
    
    const from = fromStr.split(',').map(Number) as [number, number];
    const to = toStr.split(',').map(Number) as [number, number];
    
    if (from.length !== 2 || to.length !== 2 || from.some(isNaN) || to.some(isNaN)) {
      throw new Error('Invalid coordinate format. Expected: lon,lat');
    }
    
    const avoidRects = avoidStr.split('|').filter(Boolean);
    const avoidAreas = avoidRects.length > 0 ? {
      type: 'MultiPolygon' as const,
      coordinates: avoidRects.map(rectToRing)
    } : undefined;
    
    return {
      type: 'FeatureCollection',
      features: [
        {
          type: 'Feature',
          geometry: { type: 'Point', coordinates: from },
          properties: { pointIndex: 0, pointType: 'waypoint' }
        },
        {
          type: 'Feature',
          geometry: { type: 'Point', coordinates: to },
          properties: { pointIndex: 1, pointType: 'waypoint' }
        }
      ],
      travelMode: 'driving',
      routeOutputOptions: ['routePath'],
      ...(avoidAreas && { avoidAreas })
    };
  };

  const getToken = (): Promise<string> => {
    // Get API base URL from environment
    const apiBaseUrl = import.meta.env.VITE_API_BASE_URL || '/api';
    
    return fetch(`${apiBaseUrl}/maps-token`, { credentials: 'include' })
      .then(response => {
        if (!response.ok) {
          throw new Error(`Token request failed: ${response.status} ${response.statusText}`);
        }
        return response.json();
      })
      .then(data => data.access_token);
  };

  useEffect(() => {
    const initializeMap = async () => {
      if (!mapRef.current) return;

      try {
        setLoading(true);
        setError(null);

        // Get Azure Maps Client ID from environment
        const clientId = import.meta.env.VITE_AZURE_MAPS_CLIENT_ID;
        if (!clientId) {
          throw new Error('Azure Maps Client ID not configured. Set VITE_AZURE_MAPS_CLIENT_ID environment variable.');
        }

        // Create map with anonymous auth + token callback
        const map = new atlas.Map(mapRef.current, {
          view: 'Auto',
          authOptions: {
            authType: 'anonymous' as atlas.AuthenticationType,
            clientId,
            getToken: (resolve, reject) => {
              getToken()
                .then(token => resolve(token))
                .catch(error => reject(error));
            }
          }
        });

        mapInstanceRef.current = map;

        // Wait for map to be ready
        await new Promise<void>((resolve) => {
          map.events.add('ready', () => resolve());
        });

        // Get route specification
        const spec = await getRouteSpec();

        // Get token for REST API calls
        const token = await getToken();

        // Call Azure Maps Route API
        const response = await fetch('https://atlas.microsoft.com/route/directions?api-version=2023-01-01', {
          method: 'POST',
          headers: {
            'Content-Type': 'application/geo+json',
            'Authorization': `Bearer ${token}`,
            'x-ms-client-id': clientId
          },
          body: JSON.stringify(spec)
        });

        if (!response.ok) {
          const errorText = await response.text();
          throw new Error(`Route API failed: ${response.status} ${response.statusText} - ${errorText}`);
        }

        const routeData = await response.json();

        // Add route to map
        const dataSource = new atlas.source.DataSource();
        map.sources.add(dataSource);
        dataSource.add(routeData);

        // Add route layer
        const routeLayer = new atlas.layer.LineLayer(dataSource, 'route', {
          strokeColor: '#2563eb',
          strokeWidth: 5,
          strokeOpacity: 0.8
        });
        map.layers.add(routeLayer);

        // Add avoid areas if they exist
        if (spec.avoidAreas) {
          const avoidSource = new atlas.source.DataSource();
          map.sources.add(avoidSource);
          avoidSource.add({
            type: 'Feature',
            geometry: spec.avoidAreas
          });

          const avoidLayer = new atlas.layer.PolygonLayer(avoidSource, 'avoid-areas', {
            fillColor: '#ff4444',
            fillOpacity: 0.3,
            strokeColor: '#ff0000',
            strokeWidth: 2
          });
          map.layers.add(avoidLayer);
        }

        // Add waypoint markers
        const waypointSource = new atlas.source.DataSource();
        map.sources.add(waypointSource);
        waypointSource.add(spec.features);

        const waypointLayer = new atlas.layer.SymbolLayer(waypointSource, 'waypoints', {
          iconOptions: {
            image: 'pin-round-blue',
            size: 1.2
          }
        });
        map.layers.add(waypointLayer);

        // Fit map to route bounds
        const bbox = atlas.data.BoundingBox.fromData(routeData);
        if (bbox && bbox.length >= 4) {
          map.setCamera({ 
            bounds: bbox as [number, number, number, number], 
            padding: 80 
          });
        }

        setLoading(false);
      } catch (err) {
        console.error('Map initialization error:', err);
        setError(err instanceof Error ? err.message : 'Failed to initialize map');
        setLoading(false);
      }
    };

    initializeMap();

    // Cleanup
    return () => {
      if (mapInstanceRef.current) {
        mapInstanceRef.current.dispose();
        mapInstanceRef.current = null;
      }
    };
  }, []);

  if (loading) {
    return (
      <div className="loading">
        Loading map...
      </div>
    );
  }

  if (error) {
    return (
      <div className="error">
        <h2>Error Loading Map</h2>
        <p>{error}</p>
        <p>Please check the console for more details.</p>
      </div>
    );
  }

  return (
    <div 
      ref={mapRef} 
      style={{ width: '100%', height: '100vh' }}
      aria-label="Emergency Management Route Map"
    />
  );
}