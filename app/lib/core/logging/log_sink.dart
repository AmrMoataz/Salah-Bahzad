import 'dart:developer' as developer;

import 'log_record.dart';

/// A destination for [LogRecord]s. The whole point of this seam: the app code
/// only ever talks to [AppLogger]; *where* logs go is decided by the list of
/// sinks wired in `main.dart`.
///
/// To send logs to Sentry (A3) you implement one of these — see
/// `core/logging/README.md` — and add it via `Log.configure(..., sinks: [...])`.
/// No call site changes.
abstract interface class LogSink {
  /// Emit one record. Must not throw; [AppLogger] guards the call regardless,
  /// but a sink should fail closed (drop the record) rather than propagate.
  void emit(LogRecord record);
}

/// The default sink: writes through `dart:developer.log`, so records show up in
/// the Flutter/DevTools console with their category as the `name` and the error
/// + stack trace attached. Cheap and stripped from release builds by the
/// configured level floor — never the place secrets would leak.
class ConsoleLogSink implements LogSink {
  const ConsoleLogSink();

  @override
  void emit(LogRecord record) {
    final String prefix = record.fields.isEmpty
        ? record.message
        : '${record.message} ${record.fields}';
    developer.log(
      prefix,
      time: record.timestamp,
      level: record.level.severity,
      name: record.category ?? 'app',
      error: record.error,
      stackTrace: record.stackTrace,
    );
  }
}
