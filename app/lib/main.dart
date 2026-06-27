import 'dart:async';

import 'package:firebase_core/firebase_core.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:media_kit/media_kit.dart';
import 'package:sentry_flutter/sentry_flutter.dart';
import 'package:window_manager/window_manager.dart';

import 'app/app.dart';
import 'core/logging/logging.dart';
import 'core/logging/sentry_log_sink.dart';
import 'core/platform/app_platform.dart';
import 'firebase_options.dart';

Future<void> main() async {
  const String kDsn = String.fromEnvironment('SENTRY_DSN');

  if (kDsn.isEmpty) {
    // Dev / CI: no Sentry DSN — use the guarded zone + manual error handlers.
    Log.configure(LogConfig.fromEnvironment());
    await runZonedGuarded<Future<void>>(
      () async {
        WidgetsFlutterBinding.ensureInitialized();
        Log.installErrorHandlers();
        await _boot();
      },
      (Object error, StackTrace stack) {
        Log.scoped('zone').error(
          'Uncaught async error',
          error: error,
          stackTrace: stack,
        );
      },
    );
  } else {
    // Production: Sentry owns the global error handlers + the async zone.
    // Do NOT also call Log.installErrorHandlers() — Sentry installs its own
    // FlutterError / PlatformDispatcher hooks and double-installing corrupts the
    // chain. sendDefaultPii = false ensures no device/user IDs leak in payloads
    // (NFR-APP-SEC-003 — belt); call sites redact secrets (suspenders).
    await SentryFlutter.init(
      (SentryFlutterOptions o) {
        o.dsn = kDsn;
        o.environment = const String.fromEnvironment(
          'SENTRY_ENV',
          defaultValue: 'production',
        );
        o.release = const String.fromEnvironment('APP_VERSION');
        o.sendDefaultPii = false;
      },
      appRunner: () async {
        // Upgrade the logging chain to include the Sentry sink now that the
        // SDK is initialized (breadcrumbs + exceptions from here on forward).
        Log.configure(
          LogConfig.fromEnvironment(),
          extraSinks: <SentryLogSink>[SentryLogSink()],
        );
        await _boot();
      },
    );
  }
}

/// The shared app startup body — same whether Sentry is on or off.
Future<void> _boot() async {
  MediaKit.ensureInitialized();

  final AppPlatform platform = AppPlatform();

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
}

/// Initialises Firebase with the dev project options (`firebase_options.dart`,
/// wired in the A0 wiring stream). Guarded: a misconfiguration must still let
/// the app launch to a usable screen (`NFR-APP-PERF-001`) — sign-in then fails
/// loudly at the Firebase call rather than blocking startup.
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
