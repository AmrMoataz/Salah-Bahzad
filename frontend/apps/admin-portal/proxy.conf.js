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
];
