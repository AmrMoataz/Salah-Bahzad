export const environment = {
  production: false,
  // Empty = same-origin: requests go to /api/* and the Angular dev-server proxy
  // (proxy.conf.js) forwards them to the live API. Same-origin keeps the HttpOnly
  // sb_device cookie a first-party cookie (SameSite=Lax) in dev.
  apiUrl: '',
  firebase: {
    apiKey: 'AIzaSyCtaaoO-5YSaItKDHC4kf5KPHwPLnV_Cu0',
    authDomain: 'salah-bahzad-development.firebaseapp.com',
    projectId: 'salah-bahzad-development',
    storageBucket: 'salah-bahzad-development.firebasestorage.app',
    messagingSenderId: '643096678500',
    appId: '1:643096678500:web:c49ef0d7a8d1b68717cf71',
    measurementId: 'G-T5W4DYNSRZ',
  },
};
