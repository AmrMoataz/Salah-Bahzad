import 'package:flutter/foundation.dart';

import 'log_level.dart';

/// One immutable log event handed to every [LogSink].
///
/// Security (`NFR-APP-SEC-003`): a record must **never** carry a token, refresh
/// token, signed URL, HLS key, handoff, or other PII — neither in [message] nor
/// in [fields]. Redact at the call site; sinks forward records verbatim.
@immutable
class LogRecord {
  const LogRecord({
    required this.level,
    required this.message,
    required this.timestamp,
    this.category,
    this.error,
    this.stackTrace,
    this.fields = const <String, Object?>{},
  });

  final LogLevel level;

  /// Human-readable, already-redacted summary.
  final String message;

  final DateTime timestamp;

  /// Logical scope, e.g. `auth`, `net`, `deeplink` — surfaced as the console
  /// `name` and (later) the Sentry category/breadcrumb tag.
  final String? category;

  /// The thrown object, when this record reports a failure.
  final Object? error;

  final StackTrace? stackTrace;

  /// Structured, redaction-safe context (counts, status codes, reason codes).
  final Map<String, Object?> fields;
}
