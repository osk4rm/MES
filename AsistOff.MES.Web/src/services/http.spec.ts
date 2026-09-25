import { describe, expect, it, vi } from 'vitest';
import { ensureCorrelationId } from './http';
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
