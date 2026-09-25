import { describe, expect, it, vi } from 'vitest';
import type { AxiosResponse, InternalAxiosRequestConfig } from 'axios';
import http, { buildLoginRedirectUrl, ensureCorrelationId, isAuthEndpoint } from './http';
import { CORRELATION_ID_HEADER } from './correlation';

const GUID_PATTERN =
  /^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/;

function axiosLikeHeaders(initial: Record<string, string> = {}): {
  get: (name: string) => string | null;
  set: (name: string, value: string) => void;
  [key: string]: unknown;
} {
  const store = new Map<string, string>();
  for (const [key, value] of Object.entries(initial)) {
    store.set(key.toLowerCase(), value);
  }
  return {
    get: (name: string) => store.get(name.toLowerCase()) ?? null,
    set: (name: string, value: string) => {
      store.set(name.toLowerCase(), value);
    }
  };
}

function readHeader(headers: unknown, name: string): unknown {
  if (headers !== null && typeof headers === 'object' && typeof (headers as { get?: unknown }).get === 'function') {
    return (headers as { get: (header: string) => unknown }).get(name);
  }
  const record = headers as Record<string, unknown> | undefined;
  return record?.[name] ?? record?.[name.toLowerCase()];
}

describe('ensureCorrelationId', () => {
  it('sends X-Correlation-ID on requests without one', () => {
    const headers = axiosLikeHeaders();

    const value = ensureCorrelationId(headers);

    expect(GUID_PATTERN.test(value)).toBe(true);
    expect(headers.get(CORRELATION_ID_HEADER)).toBe(value);
  });

  it('preserves a caller-supplied value instead of overwriting it', () => {
    const caller = '3f2504e0-4f89-11d3-9a0c-0305e82c3301';
    const headers = axiosLikeHeaders({ [CORRELATION_ID_HEADER]: caller });
    const generate = vi.fn(() => '11111111-1111-1111-1111-111111111111');

    const value = ensureCorrelationId(headers, generate);

    expect(value).toBe(caller);
    expect(headers.get(CORRELATION_ID_HEADER)).toBe(caller);
    expect(generate).not.toHaveBeenCalled();
  });

  it('forwards the same value across retries', () => {
    const headers = axiosLikeHeaders();

    const first = ensureCorrelationId(headers);
    const second = ensureCorrelationId(headers);

    expect(second).toBe(first);
  });
});

// Cookie transport (issue #242): API calls travel with httpOnly cookies
// (withCredentials) and must never carry an Authorization bearer header,
// because JavaScript never sees a usable token.
describe('http cookie transport', () => {
  it('sends cookies and never injects a bearer token', async () => {
    let captured: InternalAxiosRequestConfig | undefined;

    await http.get('/api/probe', {
      adapter: async (config: InternalAxiosRequestConfig): Promise<AxiosResponse> => {
        captured = config;
        return { data: {}, status: 200, statusText: 'OK', headers: {}, config };
      }
    });

    expect(captured?.withCredentials).toBe(true);
    expect(readHeader(captured?.headers, 'Authorization')).toBeUndefined();
    expect(typeof readHeader(captured?.headers, CORRELATION_ID_HEADER)).toBe('string');
  });
});

describe('isAuthEndpoint', () => {
  it('matches the cookie session endpoints', () => {
    expect(isAuthEndpoint('/api/auth/sign-in')).toBe(true);
    expect(isAuthEndpoint('/api/auth/refresh')).toBe(true);
    expect(isAuthEndpoint('/api/auth/sign-out')).toBe(true);
  });

  it('does not match domain endpoints or missing urls', () => {
    expect(isAuthEndpoint('/api/products')).toBe(false);
    expect(isAuthEndpoint(undefined)).toBe(false);
  });
});

// Expired sessions bounce to login with the original path preserved as
// ?redirect (no tokens in the URL); auth pages handle 401 themselves.
describe('buildLoginRedirectUrl', () => {
  it('preserves the current path as the redirect param', () => {
    expect(buildLoginRedirectUrl('/reports/oee', '')).toBe('/login?redirect=%2Freports%2Foee');
  });

  it('preserves query strings without leaking tokens', () => {
    const target = buildLoginRedirectUrl('/production/orders', '?page=2');

    expect(target).toBe('/login?redirect=%2Fproduction%2Forders%3Fpage%3D2');
    expect(target).not.toMatch(/token|bearer/i);
  });

  it('skips the redirect on auth pages to avoid loops', () => {
    expect(buildLoginRedirectUrl('/', '')).toBeNull();
    expect(buildLoginRedirectUrl('/login', '?redirect=%2Fdashboard')).toBeNull();
    expect(buildLoginRedirectUrl('/register', '')).toBeNull();
  });
});
