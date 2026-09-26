// Runtime API base URL resolution (issue #271).
//
// The web container renders its effective backend URL to /config.js at
// startup (docker-entrypoint.sh, from the API_BASE_URL environment
// variable). That runtime value wins over the build-time
// VITE_API_BASE_URL, which in turn wins over the documented default — so a
// production operator repoints the backend with a container restart, never
// a rebuild. An explicitly emptied value fails fast with a named message.

export const API_BASE_URL_ENV_NAME = 'API_BASE_URL';
export const DEFAULT_API_BASE_URL = 'http://localhost:8080';

export interface MesRuntimeConfig {
  apiBaseUrl?: string;
}

declare global {
  interface Window {
    __MES_CONFIG__?: MesRuntimeConfig;
  }
}

/**
 * Pure precedence helper (unit-tested): runtime > build-time > default.
 * Throws when no usable value remains.
 */
export function resolveApiBaseUrl(
  runtimeValue: string | undefined,
  buildTimeValue: string | undefined,
  defaultValue: string = DEFAULT_API_BASE_URL,
): string {
  const runtime = (runtimeValue ?? '').trim();
  if (runtime !== '') return runtime;

  const buildTime = (buildTimeValue ?? '').trim();
  if (buildTime !== '') return buildTime;

  const fallback = (defaultValue ?? '').trim();
  if (fallback !== '') return fallback;

  throw new Error(
    `${API_BASE_URL_ENV_NAME} is not configured and no default is available. ` +
      `Set the ${API_BASE_URL_ENV_NAME} environment variable for the web container ` +
      `(example: ${API_BASE_URL_ENV_NAME}=https://mes.example.com) or rebuild with VITE_API_BASE_URL. ` +
      `See docs/production-runbook.md.`,
  );
}

/** Effective base URL for the shared axios instance. */
export function currentApiBaseUrl(): string {
  const runtime = typeof window !== 'undefined' ? window.__MES_CONFIG__?.apiBaseUrl : undefined;
  const buildTime = import.meta.env.VITE_API_BASE_URL as string | undefined;
  return resolveApiBaseUrl(runtime, buildTime);
}
