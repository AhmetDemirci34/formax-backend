import type { NextConfig } from "next";
import path from "path";

const nextConfig: NextConfig = {
  // Pin workspace root so Next.js doesn't scan parent lockfiles
  turbopack: {
    root: path.resolve(__dirname),
  },

  // Proxy all /api/* requests to the .NET backend — no CORS changes needed
  async rewrites() {
    return [
      {
        source: "/api/:path*",
        destination: "http://localhost:5063/api/:path*",
      },
    ];
  },
};

export default nextConfig;
