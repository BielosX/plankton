import { defineConfig } from 'vite'

export default defineConfig({
    server: {
        open: true,
        proxy: {
            '/ws': {
                target: "ws://localhost:5000",
                ws: true,
                rewriteWsOrigin: true,
            }
        }
    }
});