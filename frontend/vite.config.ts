import { defineConfig } from 'vitest/config'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'

export default defineConfig({
  plugins: [react(), tailwindcss()],
  server: {
    port: 5173,
    // El backend sirve /api. Con este proxy el navegador ve todo en el mismo origen,
    // que es lo que permite que la cookie de sesión viaje con SameSite=Strict (FR-023).
    proxy: {
      '/api': {
        target: process.env.GT_API_URL ?? 'http://localhost:8080',
        changeOrigin: true,
      },
    },
  },
  test: {
    environment: 'jsdom',
    globals: true,
    setupFiles: ['./src/configuracionTests.ts'],
  },
})
