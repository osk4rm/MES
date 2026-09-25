import { beforeEach, describe, expect, it, vi } from 'vitest';
import http from './http';
import { refreshSession, signIn, signOut } from './authService';

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
