import { useEffect, useRef, useState } from 'react';
import * as atlas from 'azure-maps-control';
import 'azure-maps-control/dist/atlas.min.css';
import MapControls from './MapControls';
import DrivingDirections from './DrivingDirections';

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
  routeOutputOptions: string[];
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
  const [routeBounds, setRouteBounds] = useState<[number, number, number, number] | null>(null);
  const [routeData, setRouteData] = useState<any>(null);
  const [showDirections, setShowDirections] = useState(false);
  const [debugMode] = useState<boolean>(() => {
    try {
      const qp = new URLSearchParams(location.search);
      return qp.get('debug') === '1' || (globalThis as any).__FORCE_MAP_DEBUG__ === true;
    } catch {
      return false;
    }
  });

  const t0Ref = useRef<number>(performance.now());

  const debugLog = (...args: any[]) => {
    if (debugMode) {
      // eslint-disable-next-line no-console
      console.log('[MapPage]', ...args);
    }
  };

  const warnLog = (...args: any[]) => {
    // eslint-disable-next-line no-console
    console.warn('[MapPage]', ...args);
  };

  const errLog = (...args: any[]) => {
    // eslint-disable-next-line no-console
    console.error('[MapPage]', ...args);
  };

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
    
    // Resolve API base URL with precedence: runtime injected global -> build-time env -> default '/api'
    const runtimeApiBase = (globalThis as any).__API_BASE_URL__ as string | undefined;
    const apiBaseUrl = (runtimeApiBase && runtimeApiBase.length > 0 ? runtimeApiBase : import.meta.env.VITE_API_BASE_URL) || '/api';
    debugLog('API base resolution', { runtimeApiBase, buildTime: import.meta.env.VITE_API_BASE_URL, final: apiBaseUrl });
    
    if (id) {
      // Short-link flow: fetch route spec directly from public endpoint (no authentication required)
      const publicRouteUrl = `${apiBaseUrl}/public/routeLinks/${encodeURIComponent(id)}`;
      debugLog('Fetching route spec from public endpoint', { id, publicRouteUrl });
      
      const response = await fetch(publicRouteUrl, {
        credentials: 'omit' // No credentials needed for public endpoint
      }).catch(err => {
        errLog('Network error fetching route spec from public endpoint', err);
        throw err;
      });
      
      if (!response.ok) {
        const text = await response.text();
        if (response.status === 410) {
          throw new Error('This route link has expired');
        } else if (response.status === 404) {
          throw new Error('Route link not found');
        } else if (response.status === 403) {
          throw new Error('Access to this route link is not allowed from this location');
        } else {
          throw new Error(`Failed to fetch route: ${response.status} ${response.statusText} body=${text}`);
        }
      }
      
      const routeSpec = await response.json();
      debugLog('Fetched route spec from public endpoint', routeSpec);
      
      // Remove ttlMinutes as it's not needed for Azure Maps API call
      if (routeSpec.ttlMinutes !== undefined) {
        delete routeSpec.ttlMinutes;
      }
      
      return routeSpec;
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
    
    const spec: RouteSpec = {
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
      routeOutputOptions: ['routePath', 'itinerary'],
      ...(avoidAreas && { avoidAreas })
    };
    debugLog('Constructed route spec from query params', spec);
    return spec;
  };

  const getToken = (label: string = 'token'): Promise<string> => {
    const started = performance.now();
    debugLog('Requesting maps token', { label });
    // Token endpoint is handled by the web app itself, not the function app
    return fetch('/api/maps-token', { credentials: 'include' })
      .then(response => {
        if (!response.ok) {
          warnLog('Token request failed status', { status: response.status, statusText: response.statusText });
          throw new Error(`Token request failed: ${response.status} ${response.statusText}`);
        }
        return response.json();
      })
      .then(data => {
        const elapsed = (performance.now() - started).toFixed(1);
        debugLog('Received maps token', { label, ms: elapsed, hasToken: !!data?.access_token, tokenPreview: data?.access_token?.slice(0, 10) + '...' });
        return data.access_token;
      })
      .catch(err => {
        errLog('Token fetch error', { label, err });
        throw err;
      });
  };

  useEffect(() => {
    const initializeMap = async () => {
      if (!mapRef.current) return;

      try {
        setLoading(true);
        setError(null);
        debugLog('Initializing map sequence, t+ms', (performance.now() - t0Ref.current).toFixed(1));

        // Get Azure Maps Client ID from runtime config with fallback to build-time env
        const runtimeMapsClientId = (globalThis as any).__AZURE_MAPS_CLIENT_ID__ as string | undefined;
        const clientId = (runtimeMapsClientId && runtimeMapsClientId.length > 0 ? runtimeMapsClientId : import.meta.env.VITE_AZURE_MAPS_CLIENT_ID);
        if (!clientId) {
          throw new Error('Azure Maps Client ID not configured. Set AZURE_MAPS_CLIENT_ID environment variable or VITE_AZURE_MAPS_CLIENT_ID for local development.');
        }
        debugLog('Client ID resolved', { runtime: runtimeMapsClientId, build: import.meta.env.VITE_AZURE_MAPS_CLIENT_ID, final: clientId });

        // Create map with anonymous auth + token callback
        const map = new atlas.Map(mapRef.current, {
          view: 'Auto',
          authOptions: {
            authType: 'anonymous' as atlas.AuthenticationType,
            clientId,
            getToken: (resolve, reject) => {
              getToken('map-auth')
                .then(token => {
                  debugLog('Supplying token to map control');
                  resolve(token);
                })
                .catch(error => {
                  errLog('Map auth token callback failed', error);
                  reject(error);
                });
            }
          }
        });

        mapInstanceRef.current = map;

        // Register additional event listeners for diagnostics
        map.events.add('ready', () => debugLog('Map event: ready'));
        map.events.add('error', (e) => errLog('Map event: error', e));
        map.events.add('data', (e) => debugLog('Map event: data', { source: (e as any)?.source }));

        // Safety timeout if ready never fires
        const readyTimeout = setTimeout(() => {
          if (loading) {
            warnLog('Map ready event timeout after 8s – container size?', { width: mapRef.current?.clientWidth, height: mapRef.current?.clientHeight });
          }
        }, 8000);

        // Wait for map to be ready
        await new Promise<void>((resolve) => {
          map.events.add('ready', () => {
            clearTimeout(readyTimeout);
            resolve();
          });
        });
        debugLog('Map is ready', { tReadyMs: (performance.now() - t0Ref.current).toFixed(1) });

        // Get route specification
        const spec = await getRouteSpec().catch(err => {
            errLog('Failed building route spec', err);
            throw err;
        });

        // Get token for REST API calls
        const token = await getToken('route-rest');

        // Call Azure Maps Route API with updated version
        const routeCallStarted = performance.now();
        const response = await fetch('https://atlas.microsoft.com/route/directions?api-version=2025-01-01', {
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
        setRouteData(routeData); // Store route data for directions panel
        debugLog('Route API success', { ms: (performance.now() - routeCallStarted).toFixed(1), features: Array.isArray(routeData?.features) ? routeData.features.length : 'n/a' });
        if (debugMode) {
          (globalThis as any).__LAST_ROUTE_DATA__ = routeData;
          (globalThis as any).__LAST_ROUTE_SPEC__ = spec;
        }

        // Add route to map
        const dataSource = new atlas.source.DataSource();
        map.sources.add(dataSource);
        dataSource.add(routeData);
        debugLog('Added route data source');

        // Add route layer
        const routeLayer = new atlas.layer.LineLayer(dataSource, 'route', {
          strokeColor: '#2563eb',
          strokeWidth: 5,
          strokeOpacity: 0.8
        });
        map.layers.add(routeLayer);
        debugLog('Added route layer');

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
          debugLog('Added avoid area polygons', { count: spec.avoidAreas.coordinates.length });
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
        debugLog('Added waypoint layer', { count: spec.features.length });

        // Fit map to route bounds
        const bbox = atlas.data.BoundingBox.fromData(routeData);
        if (bbox && bbox.length >= 4) {
          const bounds = bbox as [number, number, number, number];
          setRouteBounds(bounds);
          map.setCamera({ 
            bounds: bounds, 
            padding: 80 
          });
          debugLog('Set camera to bounding box', { bbox });
        } else {
          warnLog('Bounding box not available or invalid', { bbox });
        }

        setLoading(false);
        debugLog('Map initialization complete', { totalMs: (performance.now() - t0Ref.current).toFixed(1) });
      } catch (err) {
        errLog('Map initialization error', err);
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

  const handleResetView = () => {
    if (mapInstanceRef.current && routeBounds) {
      mapInstanceRef.current.setCamera({
        bounds: routeBounds,
        padding: 80
      });
      debugLog('Reset view to route bounds', { bounds: routeBounds });
    }
  };

  const handleToggleDirections = (show: boolean) => {
    setShowDirections(show);
  };

  // Always render the map container so the ref exists on first effect run.
  // Present loading and error states as overlays instead of replacing the container (prevents null ref issue).
  return (
    <div style={{ position: 'relative', width: '100%', height: '100vh', display: 'flex', flexDirection: 'column' }} aria-label="Emergency Management Route Map Wrapper">
      {/* Header */}
      <header style={{
        backgroundColor: '#1e293b',
        color: '#ffffff',
        padding: '16px 24px',
        borderBottom: '2px solid #334155',
        display: 'flex',
        alignItems: 'center',
        gap: '16px',
        zIndex: 1000,
        flexShrink: 0
      }}>
        {/* Logo SVG */}
        <svg
          width="48"
          height="48"
          viewBox="0 0 48 48"
          fill="none"
          xmlns="http://www.w3.org/2000/svg"
          aria-label="Emergency Management Routing Logo"
        >
          {/* Map background */}
          <rect x="4" y="8" width="40" height="32" rx="4" fill="#374151" stroke="#6b7280" strokeWidth="1"/>
          
          {/* Road/route path */}
          <path
            d="M8 20 Q16 16 24 20 Q32 24 40 20"
            stroke="#3b82f6"
            strokeWidth="3"
            fill="none"
            strokeLinecap="round"
          />
          
          {/* Start point marker */}
          <circle cx="8" cy="20" r="3" fill="#10b981"/>
          <circle cx="8" cy="20" r="1.5" fill="#ffffff"/>
          
          {/* End point marker */}
          <circle cx="40" cy="20" r="3" fill="#10b981"/>
          <circle cx="40" cy="20" r="1.5" fill="#ffffff"/>
          
          {/* Fire/emergency icon */}
          <g transform="translate(20, 4)">
            <path
              d="M8 6 C8 6 6 8 6 10 C6 12.2 7.8 14 10 14 C12.2 14 14 12.2 14 10 C14 8 12 6 12 6 C11 7 9 7 8 6 Z"
              fill="#ef4444"
              stroke="#dc2626"
              strokeWidth="0.5"
            />
            <path
              d="M10 8 C10 8 9 9 9 10 C9 10.6 9.4 11 10 11 C10.6 11 11 10.6 11 10 C11 9 10 8 10 8 Z"
              fill="#fbbf24"
            />
          </g>
        </svg>
        
        {/* Title */}
        <h1 style={{
          margin: 0,
          fontSize: '24px',
          fontWeight: '700',
          color: '#ffffff',
          fontFamily: 'Inter, system-ui, -apple-system, sans-serif'
        }}>
          Emergency Management Routing
        </h1>
      </header>

      {/* Map Container */}
      <div
        ref={mapRef}
        style={{ width: '100%', height: '100%', background: '#0f172a', flex: 1 }}
        aria-label="Emergency Management Route Map"
      />

      {/* Map Controls */}
      {!loading && !error && (
        <MapControls 
          map={mapInstanceRef.current} 
          onResetView={handleResetView}
          showDirections={showDirections}
          onToggleDirections={handleToggleDirections}
        />
      )}

      {/* Driving Directions Panel */}
      {!loading && !error && (
        <DrivingDirections
          isVisible={showDirections}
          onClose={() => setShowDirections(false)}
          routeData={routeData}
        />
      )}

      {loading && (
        <div
          style={{
            position: 'absolute', inset: 0, display: 'flex', flexDirection: 'column',
            alignItems: 'center', justifyContent: 'center', background: 'rgba(15,23,42,0.55)',
            color: '#fff', backdropFilter: 'blur(2px)', fontSize: '1.1rem'
          }}
          role="status"
          aria-live="polite"
        >
          <div style={{ marginBottom: '0.75rem' }}>Loading map…</div>
          <div style={{ fontSize: '0.75rem', opacity: 0.8 }}>Initializing Azure Maps & route data</div>
        </div>
      )}

      {error && (
        <div
          style={{
            position: 'absolute', inset: 0, overflow: 'auto', padding: '1.25rem',
            background: 'rgba(31,41,55,0.9)', color: '#fff'
          }}
          role="alert"
        >
          <h2 style={{ marginTop: 0 }}>Error Loading Map</h2>
            <p style={{ marginTop: 0 }}>{error}</p>
            <p>Please check the console for more details.{debugMode && ' (debug mode enabled)'}</p>
            {debugMode && (
              <details style={{ marginTop: '1rem' }} open>
                <summary style={{ cursor: 'pointer' }}>Debug Environment</summary>
                <pre style={{ fontSize: '0.7rem', whiteSpace: 'pre-wrap', background: '#1e293b', padding: '0.75rem', borderRadius: 6 }}>
{JSON.stringify({
  location: location.href,
  runtimeApiBase: (globalThis as any).__API_BASE_URL__,
  runtimeMapsClientId: (globalThis as any).__AZURE_MAPS_CLIENT_ID__,
  buildEnv: {
    VITE_API_BASE_URL: import.meta.env.VITE_API_BASE_URL,
    VITE_AZURE_MAPS_CLIENT_ID: import.meta.env.VITE_AZURE_MAPS_CLIENT_ID
  }
}, null, 2)}
                </pre>
              </details>
            )}
        </div>
      )}
    </div>
  );
}