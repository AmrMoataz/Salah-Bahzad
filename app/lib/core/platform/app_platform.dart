import 'dart:io' show Platform;

import 'package:flutter/foundation.dart';

/// The four supported targets. The string [wireName] is what goes in the
/// `X-Platform` header and the `version-status` query (contract §F/§G):
/// `android | ios | windows | macos`.
enum AppTarget {
  android('android'),
  ios('ios'),
  windows('windows'),
  macos('macos');

  const AppTarget(this.wireName);

  final String wireName;
}

/// Host facts the UI + networking branch on. Overridable so tests are not tied
/// to the machine they run on.
class AppPlatform {
  AppPlatform({AppTarget? target}) : target = target ?? _detect();

  final AppTarget target;

  static AppTarget _detect() {
    if (kIsWeb) return AppTarget.android; // not a target; harmless default
    if (Platform.isIOS) return AppTarget.ios;
    if (Platform.isMacOS) return AppTarget.macos;
    if (Platform.isWindows) return AppTarget.windows;
    return AppTarget.android;
  }

  bool get isDesktop =>
      target == AppTarget.windows || target == AppTarget.macos;

  bool get isMobile => target == AppTarget.android || target == AppTarget.ios;

  bool get isWindows => target == AppTarget.windows;

  /// macOS draws the window controls on the **left** (traffic lights), Windows
  /// on the **right** (min/max/close) — the two desktop-chrome banners.
  bool get controlsOnLeft => target == AppTarget.macos;

  /// Whether the `google_sign_in` **plugin** ships here (Android/iOS/macOS —
  /// never Windows, where `authenticate()` throws). Used only to pick the
  /// credential source; whether the Google button is *offered* is decided by
  /// the identity provider (which also knows the Windows OAuth config).
  bool get googleSignInPluginSupported => target != AppTarget.windows;
}
