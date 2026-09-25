import { describe, expect, it } from 'vitest';
import { generateCorrelationId, isValidCorrelationId } from './correlation';

const GUID_PATTERN =
  /^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/;

describe('generateCorrelationId', () => {
  it('generates a valid GUID', () => {
    expect(GUID_PATTERN.test(generateCorrelationId())).toBe(true);
  });

  it('generates unique values', () => {
    expect(generateCorrelationId()).not.toBe(generateCorrelationId());
  });
});

describe('isValidCorrelationId', () => {
  it('accepts a valid GUID', () => {
    expect(isValidCorrelationId('3f2504e0-4f89-11d3-9a0c-0305e82c3301')).toBe(true);
  });

  it('rejects invalid values', () => {
    expect(isValidCorrelationId(null)).toBe(false);
    expect(isValidCorrelationId(undefined)).toBe(false);
    expect(isValidCorrelationId('')).toBe(false);
    expect(isValidCorrelationId('not-a-guid')).toBe(false);
  });
});
