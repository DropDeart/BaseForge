import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";

// Prod: statik dosyalar baseforge tool'u tarafından aynı origin'de sunulur.
// Dev: `npm run dev` (5173) + ayrı `baseforge new <servis>` (3500); /api proxy'lenir.
export default defineConfig({
  plugins: [react()],
  build: {
    outDir: "dist",
    emptyOutDir: true,
  },
  server: {
    port: 5173,
    proxy: {
      "/api": "http://localhost:3500",
    },
  },
});
