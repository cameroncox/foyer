// Local preview only (untracked): acts as the auth proxy, sending Remote-User like Tinyauth.
import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

const target = 'http://localhost:5080'
const headers = { 'Remote-User': 'cameron_example.com' }

export default defineConfig({
  plugins: [react()],
  server: { port: 5173, host: true, proxy: { '/api': { target, headers }, '/manifest.webmanifest': target } },
})
