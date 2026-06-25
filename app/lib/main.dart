import 'dart:async';

import 'package:firebase_core/firebase_core.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:media_kit/media_kit.dart';
import 'package:window_manager/window_manager.dart';

import 'app/app.dart';
import 'core/logging/logging.dart';
import 'core/platform/app_platform.dart';
import 'firebase_options.dart';

Future<void> main() async {
  // Configure logging first so even early bootstrap failures are captured.
  Log.configure(LogConfig.fromEnvironment());

  // A guarded zone catches async errors that escape the framework; the two
  // global handlers (installed below) catch sync framework + platform errors.
  // When Sentry lands (A3) it owns this wiring instead — see core/logging/README.
  await runZonedGuarded<Future<void>>(
    () async {
      WidgetsFlutterBinding.ensureInitialized();
      Log.installErrorHandlers();

      // Initialise the video engine (libmpv) once before any Player is created
      // (used by the A1 secure player).
      MediaKit.ensureInitialized();

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

      Log.scoped('boot').info(
        'Starting Secure Player',
        fields: <String, Object?>{'platform': platform.target.wireName},
      );
      runApp(const ProviderScope(child: SecurePlayerApp()));
    },
    (Object error, StackTrace stack) {
      Log.scoped(
        'zone',
      ).error('Uncaught async error', error: error, stackTrace: stack);
    },
  );
}

/// Initialises Firebase with the dev project options (`firebase_options.dart`,
/// wired in the A0 wiring stream). Guarded: a misconfiguration must still let the
/// app launch to a usable screen (`NFR-APP-PERF-001`) — sign-in then fails loudly
/// at the Firebase call rather than blocking startup. The failure is logged (not
/// silent) so a broken config is diagnosable.
Future<void> _initFirebase() async {
  try {
    await Firebase.initializeApp(
      options: DefaultFirebaseOptions.currentPlatform,
    );
  } catch (error, stack) {
    Log.scoped('boot').warning(
      'Firebase init failed; sign-in will be unavailable until fixed',
      error: error,
      stackTrace: stack,
    );
  }
}
