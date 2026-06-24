import 'package:flutter/foundation.dart';

import 'log_level.dart';

/// How logging is configured at startup, supplied via `--dart-define` (the same
/// mechanism as [AppConfig]). Nothing secret lives here.
///
/// ```
/// flutter run --dart-define=LOG_LEVEL=trace
/// flutter run --dart-define=LOG_LEVEL=warning --dart-define=LOG_CONSOLE=false
/// ```
class LogConfig {
  const LogConfig({required this.minLevel, required this.console});

  /// Records below this level are dropped everywhere.
  final LogLevel minLevel;

  /// Whether the built-in [ConsoleLogSink] is attached.
  final bool console;

  /// Reads `LOG_LEVEL` / `LOG_CONSOLE`. Defaults to `debug` in debug/profile
  /// builds and `warning` in release, so production stays quiet unless a remote
  /// sink (Sentry) is wired and asks for more.
  factory LogConfig.fromEnvironment() {
    const String rawLevel = String.fromEnvironment('LOG_LEVEL');
    final LogLevel level =
        LogLevel.tryParse(rawLevel) ??
        (kReleaseMode ? LogLevel.warning : LogLevel.debug);
    const bool console = bool.fromEnvironment(
      'LOG_CONSOLE',
      defaultValue: true,
    );
    return LogConfig(minLevel: level, console: console);
  }
}
