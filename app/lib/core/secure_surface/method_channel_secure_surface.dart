import 'package:flutter/services.dart';

import 'secure_surface.dart';

/// The production [SecureSurface] over a single `MethodChannel` per platform
/// plus the iOS `EventChannel` (master §3.1: "one `MethodChannel` per desktop
/// platform + an iOS `EventChannel`"). The native handlers live in each runner
/// (Android `MainActivity.kt`, Windows `flutter_window.cpp`, macOS
/// `MainFlutterWindow.swift`, iOS `AppDelegate.swift`) — there is **no** pub
/// dependency and **no** `pubspec.yaml` change.
///
/// Note the channel namespace uses **underscores** (`salah_bahzad/...`),
/// deliberately distinct from the deep-link scheme `salah-bahazad://`.
class MethodChannelSecureSurface implements SecureSurface {
  const MethodChannelSecureSurface();

  static const MethodChannel _channel = MethodChannel(
    'salah_bahzad/secure_surface',
  );
  static const EventChannel _events = EventChannel(
    'salah_bahzad/secure_surface/events',
  );

  @override
  Future<SecureSurfaceStatus> enable() async {
    try {
      final String? reply = await _channel.invokeMethod<String>('enable');
      return _statusFromNative(reply);
    } on MissingPluginException {
      // A runner left unwired (or a host without the handler) → treat as
      // unsupported so the COMPAT-002 gate refuses, never play unprotected.
      return SecureSurfaceStatus.unsupported;
    } on PlatformException {
      return SecureSurfaceStatus.unsupported;
    }
  }

  @override
  Future<void> disable() async {
    try {
      await _channel.invokeMethod<void>('disable');
    } on MissingPluginException {
      // Nothing to release — never throw on teardown (`FR-APP-CAP-003`).
    } on PlatformException {
      // Teardown must never throw.
    }
  }

  @override
  Stream<SecureSurfaceEvent> get captureEvents => _events
      .receiveBroadcastStream()
      .map(_eventFromNative)
      .where((SecureSurfaceEvent? e) => e != null)
      .cast<SecureSurfaceEvent>()
      // Off iOS the event handler is absent → swallow the
      // MissingPluginException so a page subscription is a clean no-op.
      .handleError((Object _) {});

  /// Native `enable`/`isSupported` reply → status. `"protected"` means the
  /// black-out is guaranteed; anything else (including `"unsupported"`, an
  /// unknown string, or `null`) is the fail-safe unsupported path.
  static SecureSurfaceStatus _statusFromNative(String? reply) {
    return reply == 'protected'
        ? SecureSurfaceStatus.protected
        : SecureSurfaceStatus.unsupported;
  }

  static SecureSurfaceEvent? _eventFromNative(dynamic raw) {
    switch (raw) {
      case 'capture_started':
        return SecureSurfaceEvent.captureStarted;
      case 'capture_stopped':
        return SecureSurfaceEvent.captureStopped;
      case 'screenshot':
        return SecureSurfaceEvent.screenshotTaken;
      default:
        return null;
    }
  }
}
