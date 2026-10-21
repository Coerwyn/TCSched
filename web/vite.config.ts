import vue from '@vitejs/plugin-vue'
import { defineConfig } from 'vite'

// https://vite.dev/config/
export default defineConfig({
  plugins: [vue()],
  server: {
    host: true, // listen on LAN so a phone can open the registration page
    port: 5173,
    // xfwd adds X-Forwarded-Host/Proto, so links the API builds (e.g. in .ics files) point
    // back at this dev server rather than the API's own port.
    proxy: { '/api': { target: 'http://localhost:5080', xfwd: true } },
  },
})
