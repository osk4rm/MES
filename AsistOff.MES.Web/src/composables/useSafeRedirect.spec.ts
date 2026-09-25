import { describe, expect, it } from 'vitest';
import { fallbackRedirect, resolveSafeRedirect } from './useSafeRedirect';

// Review follow-up on PR #244: `startsWith('/')` alone accepts
// protocol-relative URLs (`//evil.example`), so the guard must require a
// single-slash path and reject backslash variants.
describe('resolveSafeRedirect', () => {
  it('keeps an internal path', () => {
    expect(resolveSafeRedirect('/dashboard')).toBe('/dashboard');
  });

  it('keeps the root path', () => {
    expect(resolveSafeRedirect('/')).toBe('/');
  });

  it('rejects protocol-relative URLs', () => {
    expect(resolveSafeRedirect('//evil.example')).toBe(fallbackRedirect);
  });

  it('rejects slash-backslash variants', () => {
    expect(resolveSafeRedirect('/\\evil.example')).toBe(fallbackRedirect);
  });

  it('rejects backslashes inside the path', () => {
    expect(resolveSafeRedirect('/foo\\bar')).toBe(fallbackRedirect);
  });

  it('rejects absolute URLs and non-string values', () => {
    expect(resolveSafeRedirect('https://evil.example')).toBe(fallbackRedirect);
    expect(resolveSafeRedirect('dashboard')).toBe(fallbackRedirect);
    expect(resolveSafeRedirect('')).toBe(fallbackRedirect);
    expect(resolveSafeRedirect(undefined)).toBe(fallbackRedirect);
    expect(resolveSafeRedirect(null)).toBe(fallbackRedirect);
  });

  // Post-login targets (issue #242): deep board paths with their query
  // survive, while multi-value query params fall back to the board.
  it('keeps deep paths with query strings', () => {
    expect(resolveSafeRedirect('/production/orders?page=2')).toBe('/production/orders?page=2');
    expect(resolveSafeRedirect('/schedule')).toBe('/schedule');
  });

  it('rejects array query values', () => {
    expect(resolveSafeRedirect(['/dashboard'])).toBe(fallbackRedirect);
  });

  it('rejects javascript pseudo-protocol targets', () => {
    expect(resolveSafeRedirect('javascript:alert(1)')).toBe(fallbackRedirect);
  });
});
