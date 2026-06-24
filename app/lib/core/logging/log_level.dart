/// Severity of a [LogRecord], lowest to highest.
///
/// The numeric [severity] gives a total order so a logger can drop anything
/// below its configured floor, and so sinks (console today, Sentry tomorrow)
/// can map onto their own level scheme.
enum LogLevel {
  /// Very fine-grained tracing — request lifecycles, state transitions.
  trace(100, 'TRACE'),

  /// Developer diagnostics, useful while debugging but noisy in production.
  debug(500, 'DEBUG'),

  /// Notable, expected events worth a breadcrumb (sign-in, deep link handled).
  info(800, 'INFO'),

  /// Something recoverable went wrong (a refresh failed, a link was malformed).
  warning(900, 'WARN'),

  /// An unexpected failure — the thing you actually want Sentry to capture.
  error(1000, 'ERROR');

  const LogLevel(this.severity, this.label);

  /// Ordering weight; higher is more severe.
  final int severity;

  /// Short fixed-width tag used in console output.
  final String label;

  /// True when this level is at least as severe as [floor].
  bool operator >=(LogLevel floor) => severity >= floor.severity;

  /// Parses a case-insensitive level name (`trace`/`debug`/…), or `null` when
  /// the input is empty or unrecognised — lets callers fall back to a default.
  static LogLevel? tryParse(String? raw) {
    if (raw == null) return null;
    final String key = raw.trim().toLowerCase();
    if (key.isEmpty) return null;
    for (final LogLevel level in LogLevel.values) {
      if (level.name == key || level.label.toLowerCase() == key) return level;
    }
    return null;
  }
}
