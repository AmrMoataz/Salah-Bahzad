// File generated for the Salah Bahzad dev Firebase project (`salah-bahzad-development`).
//
// Wired in the Native App A0 **wiring** stream (see `main.dart`'s init comment): the app's
// `app-exchange` trades a Firebase ID token for a platform session, so the desktop runner needs
// real Firebase options to `initializeApp`. Values mirror the web app already registered in the
// project (the same config the student portal ships in `environments/environment.ts`) — the
// `apiKey` is the public client key, safe to commit (it is not a secret; the backend verifies the
// resulting ID token server-side). Desktop (Windows/macOS) reuses the web config, which is how
// FlutterFire configures Firebase C++ on desktop. Real per-platform Android/iOS apps are
// (re)generated via `flutterfire configure` at packaging time (A4).
import 'package:firebase_core/firebase_core.dart' show FirebaseOptions;
import 'package:flutter/foundation.dart'
    show defaultTargetPlatform, kIsWeb, TargetPlatform;

/// Default [FirebaseOptions] for the current platform, dev project `salah-bahzad-development`.
class DefaultFirebaseOptions {
  static FirebaseOptions get currentPlatform {
    if (kIsWeb) {
      return web;
    }
    switch (defaultTargetPlatform) {
      case TargetPlatform.android:
      case TargetPlatform.iOS:
      case TargetPlatform.macOS:
      case TargetPlatform.windows:
      // All platforms reuse the dev web config for A0 (auth only needs apiKey + projectId);
      // per-platform apps land at packaging time (A4).
      default:
        return web;
    }
  }

  static const FirebaseOptions web = FirebaseOptions(
    apiKey: 'AIzaSyCtaaoO-5YSaItKDHC4kf5KPHwPLnV_Cu0',
    appId: '1:643096678500:web:c49ef0d7a8d1b68717cf71',
    messagingSenderId: '643096678500',
    projectId: 'salah-bahzad-development',
    authDomain: 'salah-bahzad-development.firebaseapp.com',
    storageBucket: 'salah-bahzad-development.firebasestorage.app',
    measurementId: 'G-T5W4DYNSRZ',
  );
}
