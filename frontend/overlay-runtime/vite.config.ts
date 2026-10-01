import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import { gzipSync } from 'node:zlib';

export default defineConfig({ base: '/runtime/', plugins: [react(), {
  name: 'qualify-lightweight-runtime',
  generateBundle(_, bundle) {
    let compressedJsBytes = 0;
    for (const item of Object.values(bundle)) {
      if (item.type !== 'chunk') continue;
      if (Object.keys(item.modules).some(id => /frontend\/editor\/|monaco-editor|react-moveable|zustand/.test(id.replaceAll('\\', '/')))) throw new Error('Editor code entered the overlay runtime.');
      compressedJsBytes += gzipSync(item.code).length;
    }
    if (compressedJsBytes > 100 * 1024) throw new Error('Overlay runtime exceeds its 100 KiB compressed JavaScript budget.');
    this.emitFile({ type: 'asset', fileName: 'runtime-audit.json', source: JSON.stringify({ editorDependencies: false, compressedJsBytes }) });
  },
}], build: { outDir: '../../src/ExtensionSuite.Host/wwwroot/runtime', emptyOutDir: true } });
