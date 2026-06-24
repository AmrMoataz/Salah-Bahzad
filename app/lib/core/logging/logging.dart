import 'package:flutter/foundation.dart';

import 'app_logger.dart';
import 'log_config.dart';
import 'log_sink.dart';

export 'app_logger.dart';
export 'log_config.dart';
export 'log_level.dart';
export 'log_record.dart';
export 'log_sink.dart';

/// The process-wide logging entry point.
///
/// `main.dart` calls [configure] once at startup; everything else logs through
/// [root] / [scoped] (or the injected `loggerProvider`). Swapping the backend
/// — e.g. adding Sentry in A3 — is a one-line change here (pass another sink to
/// [configure]); no call site moves. See `core/logging/README.md`.
abstract final class Log {
  /// A safe default so logging works before [configure] runs (and in tests that
  /// never call it): console-only, `info` and above.
  static AppLogger _root = AppLogger(
    minLevel: LogConfig.fromEnvironment().minLevel,
    sinks: const <LogSink>[ConsoleLogSink()],
  );

  /// The root (category-less) logger.
  static AppLogger get root => _root;

  /// A logger tagged with [category] (e.g. `auth`, `net`, `deeplink`).
  static AppLogger scoped(String category) => _root.scoped(category);

  /// Installs the configured sinks and level floor. Call once, early in `main`.
  ///
  /// [extraSinks] are attached *in addition to* the built-in console sink (when
  /// [LogConfig.console] is true) — this is where a `SentryLogSink` plugs in.
  static void configure(
    LogConfig config, {
    List<LogSink> extraSinks = const <LogSink>[],
  }) {
    _root = AppLogger(
      minLevel: config.minLevel,
      sinks: <LogSink>[
        if (config.console) const ConsoleLogSink(),
        ...extraSinks,
      ],
    );
  }

  /// Routes Flutter's two global error channels into the logger so uncaught
  /// framework and platform errors are no longer silent. Call once in `main`,
  /// after [configure].
  ///
  /// Preserves any handler already installed (so the debug red-screen still
  /// shows). When Sentry's Flutter SDK is added it installs its own hooks, so
  /// only call this when running without it.
  static void installErrorHandlers() {
    final FlutterExceptionHandler? previousOnError = FlutterError.onError;
    FlutterError.onError = (FlutterErrorDetails details) {
      _root
          .scoped('flutter')
          .error(
            details.summary.toString(),
            error: details.exception,
            stackTrace: details.stack,
          );
      previousOnError?.call(details);
    };

    final bool Function(Object, StackTrace)? previousPlatformOnError =
        PlatformDispatcher.instance.onError;
    PlatformDispatcher.instance.onError = (Object error, StackTrace stack) {
      _root
          .scoped('platform')
          .error('Uncaught error', error: error, stackTrace: stack);
      return previousPlatformOnError?.call(error, stack) ?? true;
    };
  }
}
