// Local-dev placeholder for the runtime backend URL (issue #271).
// Vite serves this file at /config.js; the production container overwrites
// it at startup from the API_BASE_URL environment variable
// (docker-entrypoint.sh). Keep the value in sync with VITE_API_BASE_URL in
// .env.development.
window.__MES_CONFIG__ = { apiBaseUrl: 'http://localhost:5243/' };
