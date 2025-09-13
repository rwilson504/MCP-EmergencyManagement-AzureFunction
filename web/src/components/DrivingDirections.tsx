import { useState, useEffect } from 'react';

interface DrivingInstruction {
  message: string;
  point: {
    latitude: number;
    longitude: number;
  };
  distance?: number;
  maneuver?: string;
}

interface DrivingDirectionsProps {
  isVisible: boolean;
  onClose: () => void;
  routeData?: any; // Route data from Azure Maps API
}

export default function DrivingDirections({ isVisible, onClose, routeData }: DrivingDirectionsProps) {
  const [directions, setDirections] = useState<DrivingInstruction[]>([]);
  const [totalDistance, setTotalDistance] = useState<string>('');
  const [totalTime, setTotalTime] = useState<string>('');

  useEffect(() => {
    if (!routeData || !isVisible) return;

    try {
      // Extract directions from Azure Maps route data
      const extractedDirections: DrivingInstruction[] = [];
      let totalDistanceMeters = 0;
      let totalTimeSeconds = 0;

      if (routeData.features && Array.isArray(routeData.features)) {
        for (const feature of routeData.features) {
          if (feature.properties) {
            // Check for route summary data
            if (feature.properties.summary) {
              totalDistanceMeters = feature.properties.summary.lengthInMeters || 0;
              totalTimeSeconds = feature.properties.summary.travelTimeInSeconds || 0;
            }

            // Extract guidance/instructions
            if (feature.properties.guidance && feature.properties.guidance.instructions) {
              for (const instruction of feature.properties.guidance.instructions) {
                if (instruction.message && instruction.point) {
                  extractedDirections.push({
                    message: instruction.message,
                    point: {
                      latitude: instruction.point.latitude,
                      longitude: instruction.point.longitude
                    },
                    distance: instruction.routeOffsetInMeters,
                    maneuver: instruction.maneuver
                  });
                }
              }
            }
          }
        }
      }

      setDirections(extractedDirections);
      
      // Format distance and time
      const distanceKm = (totalDistanceMeters / 1000).toFixed(1);
      const timeMinutes = Math.round(totalTimeSeconds / 60);
      const timeHours = Math.floor(timeMinutes / 60);
      const remainingMinutes = timeMinutes % 60;
      
      setTotalDistance(`${distanceKm} km`);
      setTotalTime(timeHours > 0 ? `${timeHours}h ${remainingMinutes}m` : `${timeMinutes}m`);

    } catch (error) {
      console.error('Error extracting driving directions:', error);
      setDirections([]);
    }
  }, [routeData, isVisible]);

  if (!isVisible) return null;

  return (
    <div
      style={{
        position: 'fixed',
        top: '80px', // Start below the header
        right: '0',
        width: '400px',
        height: 'calc(100vh - 80px)', // Adjust height to account for header
        backgroundColor: '#fff',
        boxShadow: '-4px 0 12px rgba(0, 0, 0, 0.15)',
        zIndex: 1001,
        overflow: 'hidden',
        fontFamily: 'system-ui, -apple-system, sans-serif'
      }}
    >
      {/* Header */}
      <div
        style={{
          padding: '16px 20px',
          borderBottom: '2px solid #e5e7eb',
          backgroundColor: '#f9fafb',
          display: 'flex',
          justifyContent: 'space-between',
          alignItems: 'center'
        }}
      >
        <div>
          <h2 style={{ margin: 0, fontSize: '18px', fontWeight: '700', color: '#1f2937' }}>
            🧭 Driving Directions
          </h2>
          {totalDistance && totalTime && (
            <p style={{ margin: '4px 0 0 0', fontSize: '14px', color: '#6b7280' }}>
              {totalDistance} • {totalTime}
            </p>
          )}
        </div>
        <button
          onClick={onClose}
          style={{
            background: 'none',
            border: 'none',
            fontSize: '20px',
            cursor: 'pointer',
            color: '#6b7280',
            padding: '4px',
            borderRadius: '4px',
            transition: 'color 0.2s'
          }}
          onMouseEnter={(e) => e.currentTarget.style.color = '#1f2937'}
          onMouseLeave={(e) => e.currentTarget.style.color = '#6b7280'}
          title="Close directions"
          aria-label="Close directions"
        >
          ✕
        </button>
      </div>

      {/* Directions Content */}
      <div
        style={{
          height: 'calc(100vh - 80px)',
          overflow: 'auto',
          padding: '0'
        }}
      >
        {directions.length === 0 ? (
          <div
            style={{
              padding: '40px 20px',
              textAlign: 'center',
              color: '#6b7280'
            }}
          >
            <div style={{ fontSize: '48px', marginBottom: '16px' }}>🗺️</div>
            <p style={{ fontSize: '16px', margin: '0 0 8px 0' }}>No directions available</p>
            <p style={{ fontSize: '14px', margin: '0' }}>
              Route data may not contain detailed turn-by-turn instructions.
            </p>
          </div>
        ) : (
          <div style={{ padding: '0' }}>
            {directions.map((direction, index) => (
              <div
                key={index}
                style={{
                  padding: '16px 20px',
                  borderBottom: '1px solid #f3f4f6',
                  display: 'flex',
                  gap: '12px',
                  alignItems: 'flex-start'
                }}
              >
                <div
                  style={{
                    minWidth: '24px',
                    height: '24px',
                    backgroundColor: index === 0 ? '#10b981' : index === directions.length - 1 ? '#ef4444' : '#3b82f6',
                    color: '#fff',
                    borderRadius: '50%',
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'center',
                    fontSize: '12px',
                    fontWeight: 'bold',
                    flexShrink: 0,
                    marginTop: '2px'
                  }}
                >
                  {index + 1}
                </div>
                <div style={{ flex: 1 }}>
                  <p style={{ 
                    margin: '0 0 4px 0', 
                    fontSize: '14px', 
                    color: '#1f2937',
                    lineHeight: '1.4'
                  }}>
                    {direction.message}
                  </p>
                  {direction.distance !== undefined && (
                    <p style={{ 
                      margin: '0', 
                      fontSize: '12px', 
                      color: '#6b7280'
                    }}>
                      at {(direction.distance / 1000).toFixed(2)} km
                    </p>
                  )}
                </div>
              </div>
            ))}
          </div>
        )}
      </div>
    </div>
  );
}