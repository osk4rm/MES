export interface SignInRequest {
  username: string;
  password: string;
}

export interface JwtResponse {
    accessToken: string;
    refreshToken: string;
    expires: number;
}

