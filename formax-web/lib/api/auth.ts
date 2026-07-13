import apiClient from "./client";
import type { LoginRequest, RegisterRequest, AuthResponse } from "@/types/api";

export async function login(req: LoginRequest): Promise<AuthResponse> {
  const res = await apiClient.post<AuthResponse>("/api/auth/login", req);
  return res.data;
}

export async function register(req: RegisterRequest): Promise<AuthResponse> {
  const res = await apiClient.post<AuthResponse>("/api/auth/register", req);
  return res.data;
}
