// Aspire injects services__api__http__0 (or https) with the live API URL.
// Falls back to localhost:5000 for standalone dev without Aspire.
const apiTarget =
  process.env['services__api__https__0'] ||
  process.env['services__api__http__0'] ||
  'http://localhost:5000';

module.exports = [
  {
    context: ['/api'],
    target: apiTarget,
    secure: false,
    changeOrigin: true,
  },
  {
    // SignalR hubs (QuizHub, notifications seam) — proxied with WebSocket upgrade so the
    // student app's real-time features work behind the same-origin dev server (NFR-SEC-005:
    // the hub authenticates with the platform JWT, not query-string creds).
    context: ['/hubs'],
    target: apiTarget,
    secure: false,
    changeOrigin: true,
    ws: true,
  },
];
