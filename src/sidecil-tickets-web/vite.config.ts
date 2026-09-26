import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";
export default defineConfig({
  plugins: [react()],
  server: { port: 5173, proxy: { "/api": "http://localhost:5080" } },
  build: {
    outDir: "../Sidecil.Tickets.Api/wwwroot",
    emptyOutDir: true,
    rollupOptions: {
      output: {
        manualChunks: {
          mui: ["@mui/material", "@emotion/react", "@emotion/styled"],
          react: ["react", "react-dom", "react-router-dom"],
          query: ["@tanstack/react-query"],
        },
      },
    },
  },
});
