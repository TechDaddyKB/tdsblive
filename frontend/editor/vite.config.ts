import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import { fileURLToPath } from 'node:url';

export default defineConfig({
  plugins: [react()],
  base: '/editor/',
  // Monaco embeds a sanitizer as well as declaring its npm dependency. Replace
  // that embedded copy so security updates also reach the shipped editor.
  resolve: {
    alias: [{ find: /^(?:.*\/)?dompurify\/dompurify\.js$/, replacement: fileURLToPath(import.meta.resolve('dompurify')) }],
  },
  build: { outDir: '../../src/ExtensionSuite.Host/wwwroot/editor', emptyOutDir: true },
});
