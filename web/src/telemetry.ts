import { ApplicationInsights, SeverityLevel } from '@microsoft/applicationinsights-web';

// Build-time injected constant (defined in vite.config.ts). We guard for runtime safety.
declare const __APP_BUILD_VERSION__: string | undefined;

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
let handlersRegistered = false;

const buildVersion = (typeof __APP_BUILD_VERSION__ !== 'undefined' && __APP_BUILD_VERSION__) || (import.meta as any).env?.VITE_APP_VERSION || 'dev';

function registerGlobalHandlers(instance: ApplicationInsights) {
  if (handlersRegistered) return;
  // Global error handler
  window.addEventListener('error', (event) => {
    if (!event.error) return;
    instance.trackException({
      exception: event.error as any,
      severityLevel: SeverityLevel.Error,
      properties: {
        source: 'window.error',
        message: event.message,
        filename: (event as any).filename,
        lineno: (event as any).lineno,
        colno: (event as any).colno,
        buildVersion
      }
    });
  });
  // Unhandled promise rejections
  window.addEventListener('unhandledrejection', (event) => {
    const reason: any = (event as any).reason;
    instance.trackException({
      exception: reason instanceof Error ? reason : new Error(typeof reason === 'string' ? reason : 'Unhandled rejection'),
      severityLevel: SeverityLevel.Error,
      properties: {
        source: 'window.unhandledrejection',
        rejectedValueType: reason && reason.constructor ? reason.constructor.name : typeof reason,
        buildVersion
      }
    });
  });
  handlersRegistered = true;
}

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
      // Tag component version for correlation with source maps
      appId: undefined,
    }
  });
  appInsights.loadAppInsights();
  // Attach build version and any global properties
  appInsights.addTelemetryInitializer((envelope) => {
    (envelope.data as any).properties = {
      ...(envelope.data as any).properties,
      buildVersion
    };
  });
  // Establish initial page view
  appInsights.trackPageView();
  registerGlobalHandlers(appInsights);
  return appInsights;
}

export function getAppInsights() {
  return appInsights;
}

export function trackClientError(error: unknown, props?: Record<string, any>) {
  if (!appInsights) return;
  const ex = error instanceof Error ? error : new Error(typeof error === 'string' ? error : 'Unknown client error');
  appInsights.trackException({
    exception: ex as any,
    severityLevel: SeverityLevel.Error,
    properties: { buildVersion, ...props }
  });
}
