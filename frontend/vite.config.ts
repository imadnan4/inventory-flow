import path from "path"
import tailwindcss from "@tailwindcss/vite"
import react from "@vitejs/plugin-react"
import { defineConfig } from "vite"

// https://vite.dev/config/
export default defineConfig({
  plugins: [react(), tailwindcss()],
  resolve: {
    alias: {
      "@": path.resolve(__dirname, "./src"),
    },
  },
  server: {
    proxy: {
      "/api": "http://localhost:5255",
    },
  },
  build: {
    rolldownOptions: {
      output: {
        manualChunks: (id) => {
          const normalizedId = id.replaceAll("\\", "/")

          if (
            normalizedId.includes("/node_modules/react/") ||
            normalizedId.includes("/node_modules/react-dom/") ||
            normalizedId.includes("/node_modules/react-router/")
          ) {
            return "react"
          }

          if (
            normalizedId.includes("/node_modules/@base-ui/") ||
            normalizedId.includes("/node_modules/@hugeicons/")
          ) {
            return "ui"
          }

          return undefined
        },
      },
    },
  },
})
