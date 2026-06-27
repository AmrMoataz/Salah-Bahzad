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
      // macOS uses the native Firebase iOS SDK, which validates `appId` against
      // `^\d+:(ios|macos):[a-f0-9]+$`. The `web:` prefix throws an NSException
      // before Dart can catch it — so macOS needs its own per-platform options.
      case TargetPlatform.macOS:
        return macos;
      case TargetPlatform.android:
      case TargetPlatform.iOS:
      case TargetPlatform.windows:
      // Android/iOS get real per-platform configs at packaging time (A4).
      // Windows uses the Firebase C++ desktop SDK, which accepts the web config.
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

  // Mirrors macos/Runner/GoogleService-Info.plist (Firebase Apple app for bundle id
  // com.salahbahzad.securePlayer). Required because macOS rejects the `web:` appId.
  static const FirebaseOptions macos = FirebaseOptions(
    apiKey: 'AIzaSyD4VXH3GgUzp1wJOlBhqz62NGu3tsZZhPw',
    appId: '1:643096678500:ios:12d9bc384474c01417cf71',
    messagingSenderId: '643096678500',
    projectId: 'salah-bahzad-development',
    storageBucket: 'salah-bahzad-development.firebasestorage.app',
    iosClientId:
        '643096678500-b85elbstlekuf56nl8nnlga9hdkf3017.apps.googleusercontent.com',
    iosBundleId: 'com.salahbahzad.securePlayer',
  );
}
