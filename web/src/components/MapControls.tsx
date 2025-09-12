import { useState } from 'react';
import * as atlas from 'azure-maps-control';

interface MapControlsProps {
  map: atlas.Map | null;
  onResetView?: () => void;
}

interface LayerVisibility {
  route: boolean;
  waypoints: boolean;
  avoidAreas: boolean;
}

export default function MapControls({ map, onResetView }: MapControlsProps) {
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

  return (
    <div 
      style={{
        position: 'absolute',
        top: '16px',
        right: '16px',
        zIndex: 1000,
        background: 'rgba(255, 255, 255, 0.95)',
        borderRadius: '8px',
        boxShadow: '0 4px 12px rgba(0, 0, 0, 0.15)',
        backdropFilter: 'blur(8px)',
        fontFamily: 'system-ui, -apple-system, sans-serif',
        fontSize: '14px',
        maxWidth: isExpanded ? '280px' : '48px',
        transition: 'all 0.3s ease'
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
            <label style={{ display: 'block', marginBottom: '8px', fontWeight: '600', color: '#374151' }}>
              Map Style
            </label>
            <select
              value={mapStyle}
              onChange={(e) => handleStyleChange(e.target.value)}
              style={{
                width: '100%',
                padding: '6px 8px',
                border: '1px solid #d1d5db',
                borderRadius: '4px',
                fontSize: '14px',
                backgroundColor: '#fff'
              }}
            >
              {mapStyles.map(style => (
                <option key={style.value} value={style.value}>
                  {style.label}
                </option>
              ))}
            </select>
          </div>

          {/* Layer Toggles */}
          <div style={{ marginBottom: '16px' }}>
            <label style={{ display: 'block', marginBottom: '8px', fontWeight: '600', color: '#374151' }}>
              Map Layers
            </label>
            <div style={{ display: 'flex', flexDirection: 'column', gap: '6px' }}>
              {Object.entries(layerVisibility).map(([key, visible]) => (
                <label key={key} style={{ display: 'flex', alignItems: 'center', gap: '8px', cursor: 'pointer' }}>
                  <input
                    type="checkbox"
                    checked={visible}
                    onChange={() => handleLayerToggle(key as keyof LayerVisibility)}
                    style={{ cursor: 'pointer' }}
                  />
                  <span style={{ fontSize: '13px', color: '#6b7280' }}>
                    {key === 'route' ? 'Route Path' : 
                     key === 'waypoints' ? 'Waypoints' : 'Avoid Areas'}
                  </span>
                </label>
              ))}
            </div>
          </div>

          {/* Zoom Controls */}
          <div style={{ marginBottom: '16px' }}>
            <label style={{ display: 'block', marginBottom: '8px', fontWeight: '600', color: '#374151' }}>
              Zoom
            </label>
            <div style={{ display: 'flex', gap: '8px' }}>
              <button
                onClick={() => handleZoom('in')}
                style={{
                  flex: 1,
                  padding: '8px',
                  border: '1px solid #d1d5db',
                  borderRadius: '4px',
                  backgroundColor: '#fff',
                  cursor: 'pointer',
                  fontSize: '16px',
                  transition: 'background-color 0.2s'
                }}
                onMouseEnter={(e) => e.currentTarget.style.backgroundColor = '#f3f4f6'}
                onMouseLeave={(e) => e.currentTarget.style.backgroundColor = '#fff'}
                title="Zoom in"
                aria-label="Zoom in"
              >
                +
              </button>
              <button
                onClick={() => handleZoom('out')}
                style={{
                  flex: 1,
                  padding: '8px',
                  border: '1px solid #d1d5db',
                  borderRadius: '4px',
                  backgroundColor: '#fff',
                  cursor: 'pointer',
                  fontSize: '16px',
                  transition: 'background-color 0.2s'
                }}
                onMouseEnter={(e) => e.currentTarget.style.backgroundColor = '#f3f4f6'}
                onMouseLeave={(e) => e.currentTarget.style.backgroundColor = '#fff'}
                title="Zoom out"
                aria-label="Zoom out"
              >
                −
              </button>
            </div>
          </div>

          {/* Reset View Button */}
          <button
            onClick={handleResetView}
            style={{
              width: '100%',
              padding: '8px 12px',
              border: '1px solid #d1d5db',
              borderRadius: '4px',
              backgroundColor: '#fff',
              cursor: 'pointer',
              fontSize: '13px',
              fontWeight: '500',
              color: '#374151',
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