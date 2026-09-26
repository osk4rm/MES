import { beforeEach, describe, expect, it, vi } from 'vitest';
import http from './http';
import { extractSessionPermissions, refreshSession, restoreSession, signIn, signOut } from './authService';

vi.mock('./http', () => ({
  default: {
    post: vi.fn()
  }
}));

const postMock = vi.mocked(http.post);

// Cookie transport (issue #242): sign-in/out/refresh travel over httpOnly
// cookies with empty body tokens; the frontend never stores a token.
describe('authService cookie transport', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('signs in over cookies without storing tokens', async () => {
    postMock.mockResolvedValue({ data: { accessToken: '', refreshToken: '', expires: 0 } });

    const result = await signIn({ email: 'admin@dev.local', password: 'Passw0rd!' });

    expect(postMock).toHaveBeenCalledWith('/api/auth/sign-in', {
      email: 'admin@dev.local',
      password: 'Passw0rd!'
    });
    expect(result.accessToken).toBe('');
    expect(result.refreshToken).toBe('');
  });

  it('restores a session via the refresh cookie', async () => {
    postMock.mockResolvedValue({ data: {} });

    await expect(refreshSession()).resolves.toBe(true);

    expect(postMock).toHaveBeenCalledWith('/api/auth/refresh', {});
  });

  it('reports no session when refresh fails instead of throwing', async () => {
    postMock.mockRejectedValue({ response: { status: 401 } });

    await expect(refreshSession()).resolves.toBe(false);
  });

  it('signs out via cookies', async () => {
    postMock.mockResolvedValue({ data: undefined });

    await signOut();

    expect(postMock).toHaveBeenCalledWith('/api/auth/sign-out', {});
  });
});

// Permission grants from the response-body claims (issue #273): the route
// guard reads them without ever touching a token.
describe('authService session permissions', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('extracts the permissions claim list', () => {
    const result = extractSessionPermissions({
      accessToken: '',
      refreshToken: '',
      expires: 0,
      claims: { permissions: ['tenant.admin', 'production.write'] }
    });

    expect(result).toEqual(['tenant.admin', 'production.write']);
  });

  it('fails closed on missing or malformed claims', () => {
    expect(extractSessionPermissions(undefined)).toEqual([]);
    expect(extractSessionPermissions(null)).toEqual([]);
    expect(extractSessionPermissions({ accessToken: '', refreshToken: '', expires: 0 })).toEqual([]);
    expect(
      extractSessionPermissions({
        accessToken: '',
        refreshToken: '',
        expires: 0,
        claims: { permissions: ['', 42 as unknown as string] }
      })
    ).toEqual([]);
  });

  it('restores grants and email from the refresh response', async () => {
    postMock.mockResolvedValue({
      data: {
        accessToken: '',
        refreshToken: '',
        expires: 0,
        email: 'admin@dev.local',
        claims: { permissions: ['tenant.admin'] }
      }
    });

    const restored = await restoreSession();

    expect(postMock).toHaveBeenCalledWith('/api/auth/refresh', {});
    expect(restored).toEqual({ ok: true, permissions: ['tenant.admin'], email: 'admin@dev.local' });
  });

  it('reports a failed restore without grants instead of throwing', async () => {
    postMock.mockRejectedValue({ response: { status: 401 } });

    await expect(restoreSession()).resolves.toEqual({ ok: false, permissions: [] });
  });
});
