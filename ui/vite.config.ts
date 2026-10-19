import { svelte } from '@sveltejs/vite-plugin-svelte'
import { defineConfig } from 'vite'

export default defineConfig({
  plugins: [svelte()],
  server: {
    // Debug builds of the host load this URL when HOUSTON_UI_URL points at it.
    port: 5173,
    strictPort: true,
  },
  build: {
    // The WPF host serves this folder through WebView2's virtual host mapping.
    outDir: '../src/Houston.App/wwwroot',
    emptyOutDir: true,
  },
})
