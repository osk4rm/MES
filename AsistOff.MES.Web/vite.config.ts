import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'

// https://vite.dev/config/
export default defineConfig({
  plugins: [vue()],
  // Pin the dev server so the e2e harness (scripts/e2e/app.ps1) and CORS
  // origins stay consistent by construction. With strictPort a busy :5173
  // fails fast with a clear error instead of silently moving to :5174.
  server: {
    port: 5173,
    strictPort: true,
  },
})
