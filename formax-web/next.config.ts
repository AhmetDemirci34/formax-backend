import type { NextConfig } from "next";
import path from "path";

const nextConfig: NextConfig = {
  // Pin workspace root so Next.js doesn't scan parent lockfiles
  turbopack: {
    root: path.resolve(__dirname),
  },

  // BUILD ÇIKTI DİZİNİ — çalışan dev sunucusunu bozmadan doğrulama yapabilmek için
  // FORMAX_DIST_DIR ile geçici bir dizine yönlendirilebilir. Değişken verilmezse
  // varsayılan ".next" kullanılır; normal geliştirme ve dağıtım DEĞİŞMEZ.
  distDir: process.env.FORMAX_DIST_DIR || ".next",

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
