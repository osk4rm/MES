import { defineConfig } from 'vitest/config'
import vue from '@vitejs/plugin-vue'

// Vitest configuration for component tests (see testing.instructions.md).
// jsdom provides a DOM for @vue/test-utils `mount`.
export default defineConfig({
  plugins: [vue()],
  test: {
    environment: 'jsdom',
    include: ['src/**/*.spec.ts'],
    globals: false
  }
})
