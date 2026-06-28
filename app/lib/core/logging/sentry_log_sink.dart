import 'package:sentry_flutter/sentry_flutter.dart';

import 'log_level.dart';
import 'log_record.dart';
import 'log_sink.dart';

/// Routes app log records to Sentry (A3).
///
/// `error` and above → `Sentry.captureException` (appears in the Issues list).
/// Below `error` → `Sentry.addBreadcrumb` (trail leading up to the crash).
///
/// **Security** (`NFR-APP-SEC-003`): sinks forward records verbatim — never
/// include tokens / handoffs / signed-URLs / PII in log messages or `fields`.
/// The call sites already redact these (see `PlaybackRequest.toString()`).
/// `sendDefaultPii = false` in the Sentry options is the belt; this is the
/// suspenders.
class SentryLogSink implements LogSink {
  @override
  void emit(LogRecord r) {
    try {
      if (r.level >= LogLevel.error) {
        Sentry.captureException(
          r.error ?? r.message,
          stackTrace: r.stackTrace,
          hint: Hint.withMap(<String, Object?>{
            'logger': r.category ?? 'app',
            if (r.fields.isNotEmpty) ...r.fields,
          }),
        );
      } else {
        Sentry.addBreadcrumb(
          Breadcrumb(
            message: r.message,
            category: r.category,
            level: _toSentryLevel(r.level),
            data: r.fields.isEmpty
                ? null
                : Map<String, Object?>.from(r.fields),
          ),
        );
      }
    } catch (_) {
      // A sink must never throw into the logger (LogSink contract).
    }
  }

  static SentryLevel _toSentryLevel(LogLevel level) => switch (level) {
    LogLevel.trace || LogLevel.debug => SentryLevel.debug,
    LogLevel.info => SentryLevel.info,
    LogLevel.warning => SentryLevel.warning,
    LogLevel.error => SentryLevel.fatal,
  };
}
