import { defineConfig } from "vitest/config";
import path from "path";

// FORMAX · En küçük frontend test altyapısı.
// Tarayıcı ortamı (jsdom) KURULMADI: bileşenler react-dom/server ile HTML'e çizilir,
// davranış kuralları saf fonksiyonlardan sınanır. "@" yolu tsconfig ile aynıdır.
export default defineConfig({
  resolve: { alias: { "@": path.resolve(__dirname) } },
  esbuild: { jsx: "automatic" },
  test: {
    environment: "node",
    include: ["tests/**/*.test.{ts,tsx}"],
  },
});
