import react from '@vitejs/plugin-react'
import { defineConfig } from 'vitest/config'

const api = 'http://localhost:5080'

export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    proxy: {
      '/api': api,
      '/openapi': api,
      '/healthz': api,
    },
  },
  test: {
    environment: 'jsdom',
    setupFiles: ['./src/test/setup.ts'],
    // Whole-app tests type through Mantine forms; CI runners are several times slower than a
    // desktop, and a file's first test also pays for warming up.
    testTimeout: 15_000,
  },
})
