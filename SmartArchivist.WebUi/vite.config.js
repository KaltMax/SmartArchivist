import { defineConfig } from 'vite';
import plugin from '@vitejs/plugin-react';
import tailwindcss from '@tailwindcss/vite'

// https://vitejs.dev/config/
export default defineConfig({
    plugins: [
        plugin(),
        tailwindcss()],
    server: {
        // host must be true to allow access by nginx reverse proxy
        host: true,
        port: 56991,
        proxy: {
            '/api': 'http://localhost:8081',
            '/hubs': {
                target: 'http://localhost:8081',
                ws: true,
            },
        },
    }
})