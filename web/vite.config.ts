import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import path from 'node:path';

// Production build emits straight into the .NET API's wwwroot so
// `dotnet publish` picks it up via implicit Content items.
export default defineConfig({
  plugins: [react()],
  build: {
    outDir: path.resolve(__dirname, '../api/wwwroot'),
    emptyOutDir: true,
  },
  server: {
    port: 5173,
    strictPort: true,
    proxy: {
      '/api': {
        target: 'http://localhost:5000',
        changeOrigin: false,
      },
      '/events': {
        target: 'http://localhost:5000',
        changeOrigin: false,
      },
    },
  },
});
