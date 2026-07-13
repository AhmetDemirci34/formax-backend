"use client";

import {
  createContext,
  useContext,
  useState,
  useEffect,
  useCallback,
  type ReactNode,
} from "react";
import type { AuthResponse } from "@/types/api";

interface AuthState {
  token: string | null;
  userId: number | null;
  isLoggedIn: boolean;
  isHydrated: boolean;
}

interface AuthContextValue extends AuthState {
  setAuth: (res: AuthResponse) => void;
  logout: () => void;
}

const AuthContext = createContext<AuthContextValue | null>(null);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [state, setState] = useState<AuthState>({
    token: null,
    userId: null,
    isLoggedIn: false,
    isHydrated: false,
  });

  // Hydrate from localStorage on mount
  useEffect(() => {
    const token = localStorage.getItem("formax_token");
    const userId = localStorage.getItem("formax_userId");
    if (token && userId) {
      setState({ token, userId: parseInt(userId), isLoggedIn: true, isHydrated: true });
    } else {
      setState((prev) => ({ ...prev, isHydrated: true }));
    }
  }, []);

  const setAuth = useCallback((res: AuthResponse) => {
    localStorage.setItem("formax_token", res.token);
    localStorage.setItem("formax_userId", String(res.userId));
    setState({ token: res.token, userId: res.userId, isLoggedIn: true, isHydrated: true });
  }, []);

  const logout = useCallback(() => {
    localStorage.removeItem("formax_token");
    localStorage.removeItem("formax_userId");
    setState({ token: null, userId: null, isLoggedIn: false, isHydrated: true });
  }, []);

  return (
    <AuthContext.Provider value={{ ...state, setAuth, logout }}>
      {children}
    </AuthContext.Provider>
  );
}

export function useAuth(): AuthContextValue {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error("useAuth must be used inside <AuthProvider>");
  return ctx;
}
