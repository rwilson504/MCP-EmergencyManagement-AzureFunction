import { useState, useEffect } from 'react';

interface DrivingInstruction {
  message: string;
  point: {
    latitude: number;
    longitude: number;
  };
  distance?: number;         // meters
  duration?: number;         // seconds
  maneuver?: string;
  exitIdentifier?: string;   // e.g., "2A"
  signs?: string[];          // e.g., ["Portland"]
}

interface DrivingDirectionsProps {
  isVisible: boolean;
  onClose: () => void;
  routeData?: any; // Can be an array of Features (+ trailing summary) or { features: [...] }
}

// --- helpers ---
const stripTags = (html?: string) =>
  (html || '')
    .replace(/<street>(.*?)<\/street>/gi, '$1')
    .replace(/<roadNumber>(.*?)<\/roadNumber>/gi, '$1')
    .replace(/<signpostText>(.*?)<\/signpostText>/gi, '$1')
    .replace(/<exitNumber>(.*?)<\/exitNumber>/gi, '$1')
    .replace(/<[^>]+>/g, '')
    .trim();

const formatTotalTime = (seconds: number) => {
  const mins = Math.round(seconds / 60);
  const hrs = Math.floor(mins / 60);
  const rem = mins % 60;
  return hrs > 0 ? `${hrs}h ${rem}m` : `${mins}m`;
};

const metersToKmStr = (m: number) => `${(m / 1000).toFixed(1)} km`;

function normalizeToArray(data: any): any[] {
  if (!data) return [];
  if (Array.isArray(data)) return data;
  if (Array.isArray(data.features)) return data.features;
  return [];
}

export default function DrivingDirections({ isVisible, onClose, routeData }: DrivingDirectionsProps) {
  const [directions, setDirections] = useState<DrivingInstruction[]>([]);
  const [totalDistance, setTotalDistance] = useState<string>('');
  const [totalTime, setTotalTime] = useState<string>('');

  useEffect(() => {
    if (!routeData || !isVisible) return;

    try {
      const items = normalizeToArray(routeData);

      // The Azure Maps response you showed has many Feature steps and
      // a final non-Feature summary object with properties.type === "RoutePath".
      // We'll separate them.
      const features = items.filter((f) => f && f.type === 'Feature');
      const possibleSummary = items.find((f) => f && f.type !== 'Feature' && f.properties && f.properties.type === 'RoutePath');

      // Build instructions from each Feature
      const extracted: DrivingInstruction[] = features
        .map((feature: any) => {
          const p = feature?.properties || {};
          const instr = p.instruction || {};
          const text = stripTags(instr.formattedText) || instr.text || '';
          const coords = feature?.geometry?.coordinates || [undefined, undefined];
          const [lon, lat] = coords;

          if (!text || lat == null || lon == null) return null;

          return {
            message: [
              text,
              p.exitIdentifier ? ` (Exit ${p.exitIdentifier})` : '',
              p.signs && p.signs.length ? ` [Signs: ${p.signs.join(', ')}]` : ''
            ].join(''),
            point: {
              latitude: lat,
              longitude: lon
            },
            distance: typeof p.distanceInMeters === 'number' ? p.distanceInMeters : undefined,
            duration: typeof p.durationInSeconds === 'number' ? p.durationInSeconds : undefined,
            maneuver: instr.maneuverType,
            exitIdentifier: p.exitIdentifier,
            signs: p.signs
          } as DrivingInstruction;
        })
        .filter(Boolean) as DrivingInstruction[];

      setDirections(extracted);

      // Totals
      let totalMeters = 0;
      let totalSeconds = 0;

      if (possibleSummary?.properties) {
        const sp = possibleSummary.properties;
        totalMeters = typeof sp.distanceInMeters === 'number' ? sp.distanceInMeters : 0;
        totalSeconds = typeof sp.durationInSeconds === 'number' ? sp.durationInSeconds : 0;
      } else {
        // Fallback: sum from steps
        for (const d of extracted) {
          if (typeof d.distance === 'number') totalMeters += d.distance;
          if (typeof d.duration === 'number') totalSeconds += d.duration;
        }
      }

      setTotalDistance(metersToKmStr(totalMeters));
      setTotalTime(formatTotalTime(totalSeconds));
    } catch (err) {
      console.error('Error extracting driving directions:', err);
      setDirections([]);
      setTotalDistance('');
      setTotalTime('');
    }
  }, [routeData, isVisible]);

  if (!isVisible) return null;

  return (
    <div
      style={{
        position: 'fixed',
        top: '80px',
        right: '0',
        width: '400px',
        height: 'calc(100vh - 80px)',
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
          onMouseEnter={(e) => (e.currentTarget.style.color = '#1f2937')}
          onMouseLeave={(e) => (e.currentTarget.style.color = '#6b7280')}
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
              Route data may not contain detailed turn by turn instructions.
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
                    backgroundColor:
                      index === 0 ? '#10b981' : index === directions.length - 1 ? '#ef4444' : '#3b82f6',
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
                  <p
                    style={{
                      margin: '0 0 4px 0',
                      fontSize: '14px',
                      color: '#1f2937',
                      lineHeight: '1.4'
                    }}
                  >
                    {direction.message}
                  </p>

                  {/* Optional per-step details */}
                  {(direction.distance != null || direction.duration != null) && (
                    <p style={{ margin: 0, fontSize: '12px', color: '#6b7280' }}>
                      {direction.distance != null && `dist ${(direction.distance / 1000).toFixed(2)} km`}
                      {direction.distance != null && direction.duration != null && ' • '}
                      {direction.duration != null && `time ${Math.round(direction.duration / 60)} min`}
                    </p>
                  )}

                  {/* Signs / exit callouts if you want a second line */}
                  {(direction.exitIdentifier || (direction.signs && direction.signs.length)) && (
                    <p style={{ margin: '2px 0 0 0', fontSize: '12px', color: '#9ca3af' }}>
                      {direction.exitIdentifier ? `Exit ${direction.exitIdentifier}` : ''}
                      {direction.exitIdentifier && direction.signs && direction.signs.length ? ' • ' : ''}
                      {direction.signs && direction.signs.length ? `Signs: ${direction.signs.join(', ')}` : ''}
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
