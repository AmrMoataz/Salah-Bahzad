export const environment = {
  production: false,
  // Cross-origin in staging (portal ↔ API on different hosts): the device cookie is
  // issued SameSite=None; Secure by the API, and every /api call rides withCredentials
  // (see studentAuthInterceptor) so the cookie is sent. The API must allow this origin
  // with AllowCredentials() (backend §4).
  apiUrl: 'https://api.staging.salahbahzad.com',
  tenantSlug: 'salah-bahzad',
  termsVersion: 'v1',
  firebase: {
    apiKey: 'AIzaSyC0IXB7W4qBq_dmoZm2Rsn_vvkMe4ZPMqM',
    authDomain: 'salah-bahzad-staging.firebaseapp.com',
    projectId: 'salah-bahzad-staging',
    storageBucket: 'salah-bahzad-staging.firebasestorage.app',
    messagingSenderId: '480477347740',
    appId: '1:480477347740:web:09e1bc20bd34387ad99259',
    measurementId: 'G-6L24CT6Q6D',
  },
};
