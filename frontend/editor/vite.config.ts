import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

export default defineConfig({
  plugins: [react()],
  base: '/editor/',
  build: { outDir: '../../src/ExtensionSuite.Host/wwwroot/editor', emptyOutDir: true },
});
