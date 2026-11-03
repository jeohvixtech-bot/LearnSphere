// Per-environment frontend settings. Edit this file on the server after deploying —
// it is deliberately kept out of app.js so a deployment never has to touch application code.
//
// API address. Leave unset to use:
//   - http://127.0.0.1:5000/api when the page is served from localhost:3000 (local development)
//   - <same origin>/api everywhere else (production, with the reverse proxy routing /api to the backend)
//
// Set it only when the API lives on a different host, e.g.:
//   window.LEARNSPHERE_API_URL = 'https://api.learnsphere.example/api';

// Deployment: API site on port 1000, frontend on port 1002 (set 2026-10-20).
window.LEARNSPHERE_API_URL = 'http://103.233.3.146:1000/api';
