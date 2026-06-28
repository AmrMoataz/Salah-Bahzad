export const environment = {
  production: false,
  // Same-origin (matches prod): the portal's nginx reverse-proxies /api and /hubs to the internal API.
  // This keeps the device cookie (sb_device) a FIRST-PARTY cookie — a cross-origin api.* host would make it
  // a third-party SameSite=None cookie that Safari ITP blocks, silently breaking device binding.
  apiUrl: '',
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
