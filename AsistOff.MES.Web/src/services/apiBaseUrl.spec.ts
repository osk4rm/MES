import { describe, expect, it } from 'vitest';
import { DEFAULT_API_BASE_URL, resolveApiBaseUrl } from './apiBaseUrl';

describe('resolveApiBaseUrl', () => {
  it('prefers the runtime value over build-time and default', () => {
    expect(resolveApiBaseUrl('https://mes.example.com', 'http://localhost:8080')).toBe(
      'https://mes.example.com',
    );
  });

  it('falls back to the build-time value when no runtime value is set', () => {
    expect(resolveApiBaseUrl(undefined, 'http://backend:8080')).toBe('http://backend:8080');
  });

  it('falls back to the documented default when nothing is configured', () => {
    expect(resolveApiBaseUrl(undefined, undefined)).toBe(DEFAULT_API_BASE_URL);
  });

  it('trims whitespace before comparing', () => {
    expect(resolveApiBaseUrl('  https://mes.example.com  ', 'http://localhost:8080')).toBe(
      'https://mes.example.com',
    );
  });

  it('fails fast with a named message when no usable value remains', () => {
    expect(() => resolveApiBaseUrl('', '', '')).toThrow(/API_BASE_URL is not configured/);
  });
});
