import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import type { AxiosResponse, InternalAxiosRequestConfig } from 'axios';
import http, {
  CSRF_HEADER,
  ensureCsrfToken,
  getCsrfToken,
  isAuthWriteEndpoint,
  setCsrfToken
} from './http';

function okResponse(config: InternalAxiosRequestConfig, data: unknown): AxiosResponse {
  return { data, status: 200, statusText: 'OK', headers: {}, config };
}

function readHeader(headers: unknown, name: string): unknown {
  if (headers !== null && typeof headers === 'object' && typeof (headers as { get?: unknown }).get === 'function') {
    return (headers as { get: (header: string) => unknown }).get(name);
  }
  const record = headers as Record<string, unknown> | undefined;
  return record?.[name] ?? record?.[name.toLowerCase()];
}

const originalAdapter = http.defaults.adapter;

// Double-submit CSRF (issue #376): the shared instance mints the token via
// GET /api/auth/csrf once, caches it in memory, and echoes it as
// X-CSRF-Token on the sign-in/refresh/sign-out writes — and nowhere else.
describe('isAuthWriteEndpoint', () => {
  it('matches the CSRF-gated POST auth writes', () => {
    expect(isAuthWriteEndpoint('/api/auth/sign-in', 'post')).toBe(true);
    expect(isAuthWriteEndpoint('/api/auth/refresh', 'POST')).toBe(true);
    expect(isAuthWriteEndpoint('/api/auth/sign-out', 'post')).toBe(true);
  });

  it('rejects reads, the issuance endpoint, domain posts and missing values', () => {
    expect(isAuthWriteEndpoint('/api/auth/refresh', 'get')).toBe(false);
    expect(isAuthWriteEndpoint('/api/auth/csrf', 'post')).toBe(false);
    expect(isAuthWriteEndpoint('/api/auth/csrf', 'get')).toBe(false);
    expect(isAuthWriteEndpoint('/api/products', 'post')).toBe(false);
    expect(isAuthWriteEndpoint(undefined, 'post')).toBe(false);
    expect(isAuthWriteEndpoint('/api/auth/refresh', undefined)).toBe(false);
  });
});

describe('ensureCsrfToken', () => {
  beforeEach(() => {
    setCsrfToken(null);
  });

  afterEach(() => {
    setCsrfToken(null);
    http.defaults.adapter = originalAdapter;
  });

  it('fetches the token once and caches it in memory', async () => {
    let issuanceCalls = 0;
    http.defaults.adapter = async (config: InternalAxiosRequestConfig): Promise<AxiosResponse> => {
      issuanceCalls += 1;
      return okResponse(config, { csrfToken: 'minted-token' });
    };

    const first = await ensureCsrfToken();
    const second = await ensureCsrfToken();

    expect(first).toBe('minted-token');
    expect(second).toBe('minted-token');
    expect(getCsrfToken()).toBe('minted-token');
    expect(issuanceCalls).toBe(1);
  });

  it('propagates issuance failures without caching', async () => {
    http.defaults.adapter = async (config: InternalAxiosRequestConfig): Promise<AxiosResponse> => {
      return { data: {}, status: 403, statusText: 'Forbidden', headers: {}, config };
    };

    await expect(ensureCsrfToken()).rejects.toThrow();

    expect(getCsrfToken()).toBeNull();
  });
});

describe('http CSRF header wiring', () => {
  beforeEach(() => {
    setCsrfToken(null);
  });

  afterEach(() => {
    setCsrfToken(null);
    http.defaults.adapter = originalAdapter;
  });

  it('echoes the cached token on auth writes', async () => {
    setCsrfToken('seeded-token');
    let captured: InternalAxiosRequestConfig | undefined;

    await http.post('/api/auth/refresh', {}, {
      adapter: async (config: InternalAxiosRequestConfig): Promise<AxiosResponse> => {
        captured = config;
        return okResponse(config, {});
      }
    });

    expect(readHeader(captured?.headers, CSRF_HEADER)).toBe('seeded-token');
  });

  it('mints the token on demand for a cold auth write', async () => {
    let issuanceCalls = 0;
    const seen: unknown[] = [];
    http.defaults.adapter = async (config: InternalAxiosRequestConfig): Promise<AxiosResponse> => {
      if (config.url?.includes('/api/auth/csrf')) {
        issuanceCalls += 1;
        return okResponse(config, { csrfToken: 'fresh-token' });
      }
      seen.push(readHeader(config.headers, CSRF_HEADER));
      return okResponse(config, {});
    };

    await http.post('/api/auth/sign-in', { email: 'a@b.c', password: 'x' });
    await http.post('/api/auth/sign-out', {});

    expect(issuanceCalls).toBe(1);
    expect(seen).toEqual(['fresh-token', 'fresh-token']);
  });

  it('sends no CSRF header on domain endpoints', async () => {
    setCsrfToken('seeded-token');
    let captured: InternalAxiosRequestConfig | undefined;

    await http.post('/api/products', {}, {
      adapter: async (config: InternalAxiosRequestConfig): Promise<AxiosResponse> => {
        captured = config;
        return okResponse(config, {});
      }
    });

    expect(readHeader(captured?.headers, CSRF_HEADER)).toBeUndefined();
  });
});
