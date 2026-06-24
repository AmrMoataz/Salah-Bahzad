import 'log_level.dart';
import 'log_record.dart';
import 'log_sink.dart';

/// The single logging facade the app code uses. Holds a level floor and a fan
/// of [LogSink]s; everything below the floor is dropped before a record is even
/// built, so disabled levels cost almost nothing.
///
/// Get one from the global [Log] facade (`Log.root` / `Log.scoped('auth')`) or
/// inject `loggerProvider` for testability.
class AppLogger {
  AppLogger({
    required this.minLevel,
    required List<LogSink> sinks,
    this.category,
  }) : _sinks = List<LogSink>.unmodifiable(sinks);

  /// Records below this level are discarded.
  final LogLevel minLevel;

  /// Logical scope attached to every record (e.g. `auth`). `null` on the root.
  final String? category;

  final List<LogSink> _sinks;

  /// A child logger that tags every record with [category], sharing this
  /// logger's level floor and sinks.
  AppLogger scoped(String category) =>
      AppLogger(minLevel: minLevel, sinks: _sinks, category: category);

  /// True when [level] would actually be emitted — guard expensive message
  /// construction with this.
  bool isEnabled(LogLevel level) => level >= minLevel;

  void trace(String message, {Map<String, Object?>? fields}) =>
      log(LogLevel.trace, message, fields: fields);

  void debug(String message, {Map<String, Object?>? fields}) =>
      log(LogLevel.debug, message, fields: fields);

  void info(String message, {Map<String, Object?>? fields}) =>
      log(LogLevel.info, message, fields: fields);

  void warning(
    String message, {
    Object? error,
    StackTrace? stackTrace,
    Map<String, Object?>? fields,
  }) => log(
    LogLevel.warning,
    message,
    error: error,
    stackTrace: stackTrace,
    fields: fields,
  );

  void error(
    String message, {
    Object? error,
    StackTrace? stackTrace,
    Map<String, Object?>? fields,
  }) => log(
    LogLevel.error,
    message,
    error: error,
    stackTrace: stackTrace,
    fields: fields,
  );

  /// The primitive every convenience method funnels through.
  void log(
    LogLevel level,
    String message, {
    Object? error,
    StackTrace? stackTrace,
    Map<String, Object?>? fields,
  }) {
    if (!isEnabled(level)) return;
    final LogRecord record = LogRecord(
      level: level,
      message: message,
      timestamp: DateTime.now(),
      category: category,
      error: error,
      stackTrace: stackTrace,
      fields: fields ?? const <String, Object?>{},
    );
    for (final LogSink sink in _sinks) {
      // A misbehaving sink must never take down the app or starve other sinks.
      try {
        sink.emit(record);
      } catch (_) {
        // Intentionally swallowed.
      }
    }
  }
}
