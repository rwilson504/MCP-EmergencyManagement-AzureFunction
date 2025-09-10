import { ApplicationInsights } from '@microsoft/applicationinsights-web';

// Read runtime injected globals (set by /_appconfig.js) as fallback
const runtimeConnectionString = (globalThis as any).__APPINSIGHTS_CONNECTION_STRING__ as string | undefined;
const runtimeInstrumentationKey = (globalThis as any).__APPINSIGHTS_INSTRUMENTATION_KEY__ as string | undefined;

let connectionString = runtimeConnectionString || '';
if (!connectionString && runtimeInstrumentationKey) {
  connectionString = `InstrumentationKey=${runtimeInstrumentationKey}`;
}

// Allow build-time override via Vite env (e.g., import.meta.env.VITE_APPINSIGHTS_CONNECTION_STRING)
// but only use if runtime did not supply one
const buildTime = (import.meta as any).env?.VITE_APPINSIGHTS_CONNECTION_STRING;
if (!connectionString && buildTime) {
  connectionString = buildTime;
}

let appInsights: ApplicationInsights | null = null;

export function initTelemetry() {
  if (appInsights || !connectionString) {
    return appInsights; // no-op if already initialized or no connection string
  }
  appInsights = new ApplicationInsights({
    config: {
      connectionString,
      enableAutoRouteTracking: true,
      enableCorsCorrelation: true,
      disableAjaxTracking: false,
      disableFetchTracking: false,
      // Lightweight sampling; adjust as traffic grows
      samplingPercentage: 100,
    }
  });
  appInsights.loadAppInsights();
  // Establish initial page view
  appInsights.trackPageView();
  return appInsights;
}

export function getAppInsights() {
  return appInsights;
}
