import { defineConfig } from 'vitest/config'
import vue from '@vitejs/plugin-vue'

// Vitest configuration for component tests (see testing.instructions.md).
// jsdom provides a DOM for @vue/test-utils `mount`.
export default defineConfig({
  plugins: [vue()],
  test: {
    environment: 'jsdom',
    // Smoke-helper specs live next to the Playwright suite but run here:
    // they cover the pure parsing/validation logic the smoke relies on.
    include: ['src/**/*.spec.ts', 'e2e/support/*.spec.ts'],
    globals: false
  }
})
