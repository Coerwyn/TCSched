import vue from '@vitejs/plugin-vue'
import { defineConfig } from 'vite'

// https://vite.dev/config/
export default defineConfig({
  plugins: [vue()],
  server: {
    host: true, // listen on LAN so a phone can open the registration page
    port: 5173,
    proxy: { '/api': 'http://localhost:5080' },
  },
})
