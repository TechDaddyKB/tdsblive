import { defineConfig } from 'vitest/config';
import react from '@vitejs/plugin-react';

export default defineConfig({
  plugins: [react()],
  test: {
    environment: 'jsdom',
    setupFiles: ['frontend/test-setup.ts'],
    include: ['frontend/**/*.test.tsx'],
    coverage: {
      provider: 'v8',
      reporter: ['text', 'lcov'],
      include: ['frontend/**/src/**/*.{ts,tsx}'],
      exclude: ['**/*.test.tsx'],
    },
  },
});
