# Logging (`core/logging`)

A tiny, dependency-free logging seam for the Secure Player. The app code logs
through one facade; **where** logs go is decided once in `main.dart`. Adding
Sentry (master plan **A3**) is a one-line change here — no call site moves.

## Pieces

| File | Role |
|------|------|
| `log_level.dart` | `LogLevel` (`trace…error`) with a total order. |
| `log_record.dart` | `LogRecord` — one immutable, **redaction-safe** event. |
| `log_sink.dart` | `LogSink` interface + the default `ConsoleLogSink`. |
| `app_logger.dart` | `AppLogger` — the facade: level floor + sink fan-out. |
| `log_config.dart` | `LogConfig.fromEnvironment()` — `--dart-define` driven. |
| `logging.dart` | `Log` (global facade), `Log.configure`, global error handlers. Barrel. |

## Using it

```dart
import 'core/logging/logging.dart';

final log = Log.scoped('auth');           // or ref.read(loggerProvider).scoped('auth')
log.info('Sign-in succeeded');
log.warning('Refresh failed', error: e, stackTrace: s, fields: {'status': 401});
```

Inside Riverpod code prefer the injected `loggerProvider` (testable); in
bootstrap / pure-infra code the global `Log` facade is fine.

## Configuring it

Set at run/build time, same mechanism as `AppConfig`:

```
flutter run --dart-define=LOG_LEVEL=trace
flutter run --dart-define=LOG_LEVEL=warning --dart-define=LOG_CONSOLE=false
```

Defaults: `debug` in debug/profile builds, `warning` in release; console on.

## Security — never log secrets (`NFR-APP-SEC-003`)

A `LogRecord` must **never** contain a token, refresh token, signed URL, HLS
key, handoff, or PII — not in `message`, not in `fields`. Redact at the call
site (see `PlaybackRequest.toString()`, which masks the handoff). Sinks forward
records verbatim, so a leak here reaches every backend including Sentry.

## Adding Sentry later (A3)

1. Add `sentry_flutter` to `pubspec.yaml`.
2. Implement a sink:

   ```dart
   class SentryLogSink implements LogSink {
     @override
     void emit(LogRecord r) {
       if (r.level >= LogLevel.error) {
         Sentry.captureException(r.error ?? r.message, stackTrace: r.stackTrace);
       } else {
         Sentry.addBreadcrumb(Breadcrumb(
           message: r.message,
           category: r.category,
           level: _toSentryLevel(r.level),
           data: r.fields.isEmpty ? null : Map<String, Object?>.from(r.fields),
         ));
       }
     }
   }
   ```

3. Wire it in `main.dart` and let Sentry own the global handlers:

   ```dart
   await SentryFlutter.init(
     (o) => o.dsn = const String.fromEnvironment('SENTRY_DSN'),
     appRunner: () {
       Log.configure(LogConfig.fromEnvironment(), extraSinks: [SentryLogSink()]);
       // Sentry installs its own FlutterError/PlatformDispatcher hooks, so
       // do NOT also call Log.installErrorHandlers() here.
       runApp(const ProviderScope(child: SecurePlayerApp()));
     },
   );
   ```

That's it — every existing `log.info/warning/error` call now also feeds Sentry.
