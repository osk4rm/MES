import { beforeEach, describe, expect, it } from 'vitest';
import {
  AxiosError,
  type AxiosResponse,
  type InternalAxiosRequestConfig
} from 'axios';
import http, {
  abortPendingRequests,
  createRequestController,
  extractErrorMessage,
  httpResilienceOptions,
  isCanceledError,
  resolveRequestTimeout,
  DEFAULT_REQUEST_TIMEOUT_MS
} from './http';
import { CORRELATION_ID_HEADER } from './correlation';

const GUID_PATTERN =
  /^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/;

function okResponse(config: InternalAxiosRequestConfig): AxiosResponse {
  return { data: { ok: true }, status: 200, statusText: 'OK', headers: {}, config };
}

function statusFailure(
  config: InternalAxiosRequestConfig,
  status: number
): AxiosError {
  return new AxiosError(
    `Request failed with status code ${status}`,
    'ERR_BAD_RESPONSE',
    config,
    {},
    { data: {}, status, statusText: 'Error', headers: {}, config }
  );
}

// Covers the axios hardening of issue #273: a default timeout around 15s
// (configurable via env), exactly one retry with backoff for idempotent GET
// on network failure / timeout / 502-504 with the same X-Correlation-ID,
// and no auto-retry for mutations, aborts, or other statuses.
describe('http request timeout', () => {
  it('defaults to 15s and honours VITE_API_TIMEOUT_MS', () => {
    expect(DEFAULT_REQUEST_TIMEOUT_MS).toBe(15000);
    expect(resolveRequestTimeout({})).toBe(15000);
    expect(resolveRequestTimeout({ VITE_API_TIMEOUT_MS: '8000' })).toBe(8000);
    expect(resolveRequestTimeout({ VITE_API_TIMEOUT_MS: 'nope' })).toBe(15000);
    expect(resolveRequestTimeout({ VITE_API_TIMEOUT_MS: '0' })).toBe(15000);
  });

  it('configures the shared instance with the default timeout', () => {
    expect(http.defaults.timeout).toBe(15000);
  });

  it('surfaces a timeout failure through extractErrorMessage instead of hanging', () => {
    const err = new AxiosError('timeout of 15000ms exceeded', 'ECONNABORTED');

    expect(extractErrorMessage(err, 'Load failed')).toContain('timeout');
  });
});

describe('http GET retry', () => {
  beforeEach(() => {
    httpResilienceOptions.retryDelayMs = 0;
  });

  it('retries a failed GET once on 502/503/504 with the same correlation id', async () => {
    const seen: Array<string | undefined> = [];
    let calls = 0;

    const response = await http.get('/api/probe', {
      adapter: async (config: InternalAxiosRequestConfig): Promise<AxiosResponse> => {
        calls += 1;
        seen.push(config.headers.get(CORRELATION_ID_HEADER) as string | undefined);
        if (calls === 1) throw statusFailure(config, 503);
        return okResponse(config);
      }
    });

    expect(calls).toBe(2);
    expect(response.data).toEqual({ ok: true });
    expect(seen[0]).toMatch(GUID_PATTERN);
    expect(seen[1]).toBe(seen[0]);
  });

  it('surfaces the error after the single retry still fails', async () => {
    let calls = 0;

    await expect(
      http.get('/api/probe', {
        adapter: async (config: InternalAxiosRequestConfig): Promise<AxiosResponse> => {
          calls += 1;
          throw statusFailure(config, 504);
        }
      })
    ).rejects.toMatchObject({ response: { status: 504 } });

    expect(calls).toBe(2);
  });

  it('retries once on a network failure without a response', async () => {
    let calls = 0;

    const response = await http.get('/api/probe', {
      adapter: async (config: InternalAxiosRequestConfig): Promise<AxiosResponse> => {
        calls += 1;
        if (calls === 1) throw new AxiosError('Network Error', 'ERR_NETWORK', config);
        return okResponse(config);
      }
    });

    expect(calls).toBe(2);
    expect(response.data).toEqual({ ok: true });
  });

  it('retries once on a timeout', async () => {
    let calls = 0;

    const response = await http.get('/api/probe', {
      adapter: async (config: InternalAxiosRequestConfig): Promise<AxiosResponse> => {
        calls += 1;
        if (calls === 1) throw new AxiosError('timeout of 15000ms exceeded', 'ECONNABORTED', config);
        return okResponse(config);
      }
    });

    expect(calls).toBe(2);
    expect(response.data).toEqual({ ok: true });
  });

  it('never auto-retries POST, PUT, PATCH or DELETE', async () => {
    const failingAdapter = async (config: InternalAxiosRequestConfig): Promise<AxiosResponse> => {
      calls += 1;
      throw statusFailure(config, 503);
    };
    let calls = 0;

    for (const act of [
      () => http.post('/api/probe', {}, { adapter: failingAdapter }),
      () => http.put('/api/probe', {}, { adapter: failingAdapter }),
      () => http.patch('/api/probe', {}, { adapter: failingAdapter }),
      () => http.delete('/api/probe', { adapter: failingAdapter })
    ]) {
      calls = 0;

      await expect(act()).rejects.toMatchObject({ response: { status: 503 } });

      expect(calls).toBe(1);
    }
  });

  it('never retries aborted requests or non-retryable statuses', async () => {
    let aborted = 0;
    await expect(
      http.get('/api/probe', {
        adapter: async (config: InternalAxiosRequestConfig): Promise<AxiosResponse> => {
          aborted += 1;
          throw new AxiosError('canceled', 'ERR_CANCELED', config);
        }
      })
    ).rejects.toSatisfy(isCanceledError);
    expect(aborted).toBe(1);

    let badRequest = 0;
    await expect(
      http.get('/api/probe', {
        adapter: async (config: InternalAxiosRequestConfig): Promise<AxiosResponse> => {
          badRequest += 1;
          throw statusFailure(config, 400);
        }
      })
    ).rejects.toMatchObject({ response: { status: 400 } });
    expect(badRequest).toBe(1);
  });
});

describe('tracked request cancellation', () => {
  it('aborts tracked controllers on route change without touching new ones', () => {
    const stale = createRequestController();

    abortPendingRequests();

    expect(stale.signal.aborted).toBe(true);

    const fresh = createRequestController();
    expect(fresh.signal.aborted).toBe(false);
    fresh.abort();
  });
});
