import { useState } from 'react';
import * as atlas from 'azure-maps-control';

interface MapControlsProps {
  map: atlas.Map | null;
  onResetView?: () => void;
  showDirections: boolean;
  onToggleDirections: (show: boolean) => void;
}

interface LayerVisibility {
  route: boolean;
  waypoints: boolean;
  avoidAreas: boolean;
}

export default function MapControls({ map, onResetView, showDirections, onToggleDirections }: MapControlsProps) {
  const [mapStyle, setMapStyle] = useState<string>('road');
  const [layerVisibility, setLayerVisibility] = useState<LayerVisibility>({
    route: true,
    waypoints: true,
    avoidAreas: true
  });
  const [isExpanded, setIsExpanded] = useState(false);

  const mapStyles = [
    { value: 'road', label: 'Road' },
    { value: 'satellite', label: 'Satellite' },
    { value: 'satellite_road_labels', label: 'Hybrid' },
    { value: 'grayscale_dark', label: 'Dark' },
    { value: 'grayscale_light', label: 'Light' },
    { value: 'night', label: 'Night' }
  ];

  const handleStyleChange = (newStyle: string) => {
    if (!map) return;
    
    setMapStyle(newStyle);
    map.setStyle({ style: newStyle as any });
  };

  const handleLayerToggle = (layerName: keyof LayerVisibility) => {
    if (!map) return;

    const newVisibility = {
      ...layerVisibility,
      [layerName]: !layerVisibility[layerName]
    };
    setLayerVisibility(newVisibility);

    // Toggle layer visibility in the map
    // Azure Maps uses different APIs for layer management
    const layers = map.layers.getLayers();
    let targetLayer: atlas.layer.Layer | null = null;
    
    // Find the layer by checking the layer IDs
    for (const layer of layers) {
      const id = (layer as any).getId?.();
      if ((layerName === 'route' && id === 'route') ||
          (layerName === 'waypoints' && id === 'waypoints') ||
          (layerName === 'avoidAreas' && id === 'avoid-areas')) {
        targetLayer = layer;
        break;
      }
    }

    if (targetLayer) {
      // Use setOptions to control visibility
      (targetLayer as any).setOptions({
        visible: newVisibility[layerName]
      });
    }
  };

  const handleZoom = (direction: 'in' | 'out') => {
    if (!map) return;
    
    const currentZoom = map.getCamera().zoom || 10;
    const newZoom = direction === 'in' ? currentZoom + 1 : currentZoom - 1;
    map.setCamera({ zoom: Math.max(1, Math.min(20, newZoom)) });
  };

  const handleResetView = () => {
    if (onResetView) {
      onResetView();
    }
  };

  const handlePitch = (direction: 'up' | 'down') => {
    if (!map) return;
    
    const currentPitch = map.getCamera().pitch || 0;
    const newPitch = direction === 'up' ? currentPitch + 15 : currentPitch - 15;
    map.setCamera({ pitch: Math.max(0, Math.min(60, newPitch)) });
  };

  const handleRotate = (direction: 'left' | 'right') => {
    if (!map) return;
    
    const currentBearing = map.getCamera().bearing || 0;
    const newBearing = direction === 'right' ? currentBearing + 45 : currentBearing - 45;
    map.setCamera({ bearing: newBearing });
  };

  const handleToggleDirections = () => {
    onToggleDirections(!showDirections);
  };

  return (
    <div 
      style={{
        position: 'absolute',
        top: '96px', // Account for header height (80px) + margin (16px)
        right: showDirections ? '416px' : '16px', // Adjust position when directions panel is open
        zIndex: 1000,
        background: 'rgba(255, 255, 255, 0.98)',
        borderRadius: '8px',
        boxShadow: '0 4px 20px rgba(0, 0, 0, 0.15)',
        backdropFilter: 'blur(10px)',
        fontFamily: 'system-ui, -apple-system, sans-serif',
        fontSize: '14px',
        maxWidth: isExpanded ? '300px' : '48px',
        transition: 'all 0.3s ease',
        border: '1px solid rgba(0, 0, 0, 0.05)'
      }}
    >
      {/* Control Toggle Button */}
      <button
        onClick={() => setIsExpanded(!isExpanded)}
        style={{
          width: '48px',
          height: '48px',
          border: 'none',
          background: 'transparent',
          cursor: 'pointer',
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
          fontSize: '18px',
          borderRadius: '8px',
          transition: 'background-color 0.2s'
        }}
        onMouseEnter={(e) => e.currentTarget.style.backgroundColor = 'rgba(0, 0, 0, 0.05)'}
        onMouseLeave={(e) => e.currentTarget.style.backgroundColor = 'transparent'}
        title={isExpanded ? 'Hide map controls' : 'Show map controls'}
        aria-label={isExpanded ? 'Hide map controls' : 'Show map controls'}
      >
        {isExpanded ? '✕' : '⚙️'}
      </button>

      {/* Expanded Controls */}
      {isExpanded && (
        <div style={{ padding: '16px', paddingTop: '8px' }}>
          {/* Map Style Selector */}
          <div style={{ marginBottom: '16px' }}>
            <label style={{ display: 'block', marginBottom: '8px', fontWeight: '600', color: '#1f2937' }}>
              Map Style
            </label>
            <select
              value={mapStyle}
              onChange={(e) => handleStyleChange(e.target.value)}
              style={{
                width: '100%',
                padding: '8px 12px',
                border: '2px solid #d1d5db',
                borderRadius: '6px',
                fontSize: '14px',
                backgroundColor: '#fff',
                color: '#1f2937',
                fontWeight: '500'
              }}
            >
              {mapStyles.map(style => (
                <option key={style.value} value={style.value} style={{ color: '#1f2937' }}>
                  {style.label}
                </option>
              ))}
            </select>
          </div>

          {/* Layer Toggles */}
          <div style={{ marginBottom: '16px' }}>
            <label style={{ display: 'block', marginBottom: '8px', fontWeight: '600', color: '#1f2937' }}>
              Map Layers
            </label>
            <div style={{ display: 'flex', flexDirection: 'column', gap: '8px' }}>
              {Object.entries(layerVisibility).map(([key, visible]) => (
                <label key={key} style={{ display: 'flex', alignItems: 'center', gap: '10px', cursor: 'pointer', padding: '4px' }}>
                  <input
                    type="checkbox"
                    checked={visible}
                    onChange={() => handleLayerToggle(key as keyof LayerVisibility)}
                    style={{ 
                      cursor: 'pointer',
                      width: '16px',
                      height: '16px',
                      accentColor: '#2563eb'
                    }}
                  />
                  <span style={{ fontSize: '14px', color: '#1f2937', fontWeight: '500' }}>
                    {key === 'route' ? 'Route Path' : 
                     key === 'waypoints' ? 'Waypoints' : 'Avoid Areas'}
                  </span>
                </label>
              ))}
            </div>
          </div>

          {/* Zoom Controls */}
          <div style={{ marginBottom: '16px' }}>
            <label style={{ display: 'block', marginBottom: '8px', fontWeight: '600', color: '#1f2937' }}>
              Zoom
            </label>
            <div style={{ display: 'flex', gap: '8px' }}>
              <button
                onClick={() => handleZoom('in')}
                style={{
                  flex: 1,
                  padding: '10px',
                  border: '2px solid #d1d5db',
                  borderRadius: '6px',
                  backgroundColor: '#fff',
                  cursor: 'pointer',
                  fontSize: '18px',
                  fontWeight: 'bold',
                  color: '#1f2937',
                  transition: 'all 0.2s'
                }}
                onMouseEnter={(e) => {
                  e.currentTarget.style.backgroundColor = '#f3f4f6';
                  e.currentTarget.style.borderColor = '#9ca3af';
                }}
                onMouseLeave={(e) => {
                  e.currentTarget.style.backgroundColor = '#fff';
                  e.currentTarget.style.borderColor = '#d1d5db';
                }}
                title="Zoom in"
                aria-label="Zoom in"
              >
                +
              </button>
              <button
                onClick={() => handleZoom('out')}
                style={{
                  flex: 1,
                  padding: '10px',
                  border: '2px solid #d1d5db',
                  borderRadius: '6px',
                  backgroundColor: '#fff',
                  cursor: 'pointer',
                  fontSize: '18px',
                  fontWeight: 'bold',
                  color: '#1f2937',
                  transition: 'all 0.2s'
                }}
                onMouseEnter={(e) => {
                  e.currentTarget.style.backgroundColor = '#f3f4f6';
                  e.currentTarget.style.borderColor = '#9ca3af';
                }}
                onMouseLeave={(e) => {
                  e.currentTarget.style.backgroundColor = '#fff';
                  e.currentTarget.style.borderColor = '#d1d5db';
                }}
                title="Zoom out"
                aria-label="Zoom out"
              >
                −
              </button>
            </div>
          </div>

          {/* Pitch Controls */}
          <div style={{ marginBottom: '16px' }}>
            <label style={{ display: 'block', marginBottom: '8px', fontWeight: '600', color: '#1f2937' }}>
              Pitch (Tilt)
            </label>
            <div style={{ display: 'flex', gap: '8px' }}>
              <button
                onClick={() => handlePitch('up')}
                style={{
                  flex: 1,
                  padding: '10px',
                  border: '2px solid #d1d5db',
                  borderRadius: '6px',
                  backgroundColor: '#fff',
                  cursor: 'pointer',
                  fontSize: '14px',
                  fontWeight: '500',
                  color: '#1f2937',
                  transition: 'all 0.2s'
                }}
                onMouseEnter={(e) => {
                  e.currentTarget.style.backgroundColor = '#f3f4f6';
                  e.currentTarget.style.borderColor = '#9ca3af';
                }}
                onMouseLeave={(e) => {
                  e.currentTarget.style.backgroundColor = '#fff';
                  e.currentTarget.style.borderColor = '#d1d5db';
                }}
                title="Increase pitch (tilt up)"
                aria-label="Increase pitch"
              >
                ↗ Up
              </button>
              <button
                onClick={() => handlePitch('down')}
                style={{
                  flex: 1,
                  padding: '10px',
                  border: '2px solid #d1d5db',
                  borderRadius: '6px',
                  backgroundColor: '#fff',
                  cursor: 'pointer',
                  fontSize: '14px',
                  fontWeight: '500',
                  color: '#1f2937',
                  transition: 'all 0.2s'
                }}
                onMouseEnter={(e) => {
                  e.currentTarget.style.backgroundColor = '#f3f4f6';
                  e.currentTarget.style.borderColor = '#9ca3af';
                }}
                onMouseLeave={(e) => {
                  e.currentTarget.style.backgroundColor = '#fff';
                  e.currentTarget.style.borderColor = '#d1d5db';
                }}
                title="Decrease pitch (tilt down)"
                aria-label="Decrease pitch"
              >
                ↙ Down
              </button>
            </div>
          </div>

          {/* Rotate Controls */}
          <div style={{ marginBottom: '16px' }}>
            <label style={{ display: 'block', marginBottom: '8px', fontWeight: '600', color: '#1f2937' }}>
              Rotation
            </label>
            <div style={{ display: 'flex', gap: '8px' }}>
              <button
                onClick={() => handleRotate('left')}
                style={{
                  flex: 1,
                  padding: '10px',
                  border: '2px solid #d1d5db',
                  borderRadius: '6px',
                  backgroundColor: '#fff',
                  cursor: 'pointer',
                  fontSize: '14px',
                  fontWeight: '500',
                  color: '#1f2937',
                  transition: 'all 0.2s'
                }}
                onMouseEnter={(e) => {
                  e.currentTarget.style.backgroundColor = '#f3f4f6';
                  e.currentTarget.style.borderColor = '#9ca3af';
                }}
                onMouseLeave={(e) => {
                  e.currentTarget.style.backgroundColor = '#fff';
                  e.currentTarget.style.borderColor = '#d1d5db';
                }}
                title="Rotate left"
                aria-label="Rotate left"
              >
                ↺ Left
              </button>
              <button
                onClick={() => handleRotate('right')}
                style={{
                  flex: 1,
                  padding: '10px',
                  border: '2px solid #d1d5db',
                  borderRadius: '6px',
                  backgroundColor: '#fff',
                  cursor: 'pointer',
                  fontSize: '14px',
                  fontWeight: '500',
                  color: '#1f2937',
                  transition: 'all 0.2s'
                }}
                onMouseEnter={(e) => {
                  e.currentTarget.style.backgroundColor = '#f3f4f6';
                  e.currentTarget.style.borderColor = '#9ca3af';
                }}
                onMouseLeave={(e) => {
                  e.currentTarget.style.backgroundColor = '#fff';
                  e.currentTarget.style.borderColor = '#d1d5db';
                }}
                title="Rotate right"
                aria-label="Rotate right"
              >
                ↻ Right
              </button>
            </div>
          </div>

          {/* Directions Toggle */}
          <div style={{ marginBottom: '16px' }}>
            <button
              onClick={handleToggleDirections}
              style={{
                width: '100%',
                padding: '12px',
                border: '2px solid #2563eb',
                borderRadius: '6px',
                backgroundColor: showDirections ? '#2563eb' : '#fff',
                color: showDirections ? '#fff' : '#2563eb',
                cursor: 'pointer',
                fontSize: '14px',
                fontWeight: '600',
                transition: 'all 0.2s',
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                gap: '8px'
              }}
              onMouseEnter={(e) => {
                if (!showDirections) {
                  e.currentTarget.style.backgroundColor = '#eff6ff';
                }
              }}
              onMouseLeave={(e) => {
                if (!showDirections) {
                  e.currentTarget.style.backgroundColor = '#fff';
                }
              }}
              title={showDirections ? 'Hide driving directions' : 'Show driving directions'}
              aria-label={showDirections ? 'Hide driving directions' : 'Show driving directions'}
            >
              🧭 {showDirections ? 'Hide' : 'Show'} Directions
            </button>
          </div>

          {/* Reset View Button */}
          <button
            onClick={handleResetView}
            style={{
              width: '100%',
              padding: '10px 12px',
              border: '2px solid #d1d5db',
              borderRadius: '6px',
              backgroundColor: '#fff',
              cursor: 'pointer',
              fontSize: '14px',
              fontWeight: '600',
              color: '#1f2937',
              transition: 'all 0.2s'
            }}
            onMouseEnter={(e) => {
              e.currentTarget.style.backgroundColor = '#f9fafb';
              e.currentTarget.style.borderColor = '#9ca3af';
            }}
            onMouseLeave={(e) => {
              e.currentTarget.style.backgroundColor = '#fff';
              e.currentTarget.style.borderColor = '#d1d5db';
            }}
            title="Reset view to show full route"
            aria-label="Reset view to show full route"
          >
            🎯 Reset View
          </button>
        </div>
      )}
    </div>
  );
}