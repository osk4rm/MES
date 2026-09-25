export interface SignInRequest {
  email: string;
  password: string;
}

export interface JwtResponse {
    // Cookie transport (issue #242): the backend always returns empty
    // token strings here — the session lives in httpOnly cookies.
    // Never persist these values; they are kept only for contract shape.
    accessToken: string;
    refreshToken: string;
    expires: number;
}

