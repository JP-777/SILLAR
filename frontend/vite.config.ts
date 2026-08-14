import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

// El frontend corre en :5173 y el API en :5080. Eso NO se resuelve con CORS: se
// resuelve con este proxy, para que el navegador vea un solo origen.
//
// Así la cookie de sesión viaja sin ceremonia y no hace falta relajar SameSite
// ni configurar Access-Control-Allow-Credentials. En producción ambos se sirven
// tras el mismo dominio, de modo que desarrollo y producción se comportan igual.
const apiOrigin = process.env.SILLAR_API_ORIGIN ?? 'http://localhost:5080';

export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    proxy: {
      '/api': apiOrigin,
      // La ruta estática de los medios vive fuera del API (ADR-011), así que
      // necesita su propia entrada o las imágenes no se ven en desarrollo.
      '/media': apiOrigin,
    },
  },
  build: {
    outDir: 'dist',
    sourcemap: true,
  },
});
