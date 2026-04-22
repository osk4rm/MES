export interface SignInRequest {
  email: string;
  password: string;
}

export interface JwtResponse {
    accessToken: string;
    refreshToken: string;
    expires: number;
}

