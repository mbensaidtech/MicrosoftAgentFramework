import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  server: {
    port: 3000,
    proxy: {
      '/api': {
        target: 'http://localhost:5016',
        changeOrigin: true,
      },
      '/a2a': {
        target: 'http://localhost:5016',
        changeOrigin: true,
      },
    },
  },
})
