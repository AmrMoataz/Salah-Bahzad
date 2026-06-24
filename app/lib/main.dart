import 'package:firebase_core/firebase_core.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:window_manager/window_manager.dart';

import 'app/app.dart';
import 'core/platform/app_platform.dart';
import 'firebase_options.dart';

Future<void> main() async {
  WidgetsFlutterBinding.ensureInitialized();

  final AppPlatform platform = AppPlatform();

  // Desktop: frameless window + custom chrome (the title bar is drawn in-app).
  if (platform.isDesktop) {
    await windowManager.ensureInitialized();
    const WindowOptions options = WindowOptions(
      size: Size(1180, 720),
      minimumSize: Size(420, 600),
      center: true,
      backgroundColor: Colors.transparent,
      titleBarStyle: TitleBarStyle.hidden,
    );
    await windowManager.waitUntilReadyToShow(options, () async {
      await windowManager.show();
      await windowManager.focus();
    });
  }

  await _initFirebase();

  runApp(const ProviderScope(child: SecurePlayerApp()));
}

/// Initialises Firebase with the dev project options (`firebase_options.dart`,
/// wired in the A0 wiring stream). Guarded: a misconfiguration must still let the
/// app launch to a usable screen (`NFR-APP-PERF-001`) — sign-in then fails loudly
/// at the Firebase call rather than blocking startup.
Future<void> _initFirebase() async {
  try {
    await Firebase.initializeApp(
      options: DefaultFirebaseOptions.currentPlatform,
    );
  } catch (_) {
    // Intentionally swallowed — see doc comment.
  }
}
