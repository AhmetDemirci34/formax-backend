"use client";

import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { useState, type ReactNode } from "react";
import { AuthProvider } from "./AuthContext";
import { ChromeProvider } from "./ChromeContext";

export function Providers({ children }: { children: ReactNode }) {
  // One QueryClient per browser session
  const [queryClient] = useState(
    () =>
      new QueryClient({
        defaultOptions: {
          queries: {
            staleTime: 60_000,        // 1 min default; overridden per hook
            retry: 1,
            refetchOnWindowFocus: false,
          },
        },
      })
  );

  return (
    <QueryClientProvider client={queryClient}>
      <AuthProvider>
        <ChromeProvider>{children}</ChromeProvider>
      </AuthProvider>
    </QueryClientProvider>
  );
}
