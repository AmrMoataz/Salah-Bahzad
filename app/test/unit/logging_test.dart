import 'package:flutter_test/flutter_test.dart';
import 'package:secure_player/core/logging/logging.dart';

/// Collects every record it is given — the testing seam that a Sentry sink will
/// occupy in production.
class _CapturingSink implements LogSink {
  final List<LogRecord> records = <LogRecord>[];

  @override
  void emit(LogRecord record) => records.add(record);
}

class _ThrowingSink implements LogSink {
  @override
  void emit(LogRecord record) => throw StateError('boom');
}

void main() {
  group('LogLevel', () {
    test('orders by severity', () {
      expect(LogLevel.error >= LogLevel.warning, isTrue);
      expect(LogLevel.debug >= LogLevel.info, isFalse);
    });

    test('tryParse is case-insensitive and tolerant', () {
      expect(LogLevel.tryParse('WARNING'), LogLevel.warning);
      expect(LogLevel.tryParse('trace'), LogLevel.trace);
      expect(LogLevel.tryParse('  Error '), LogLevel.error);
      expect(LogLevel.tryParse(''), isNull);
      expect(LogLevel.tryParse('nonsense'), isNull);
      expect(LogLevel.tryParse(null), isNull);
    });
  });

  group('AppLogger', () {
    test('fans every emitted record out to all sinks', () {
      final a = _CapturingSink();
      final b = _CapturingSink();
      final logger = AppLogger(
        minLevel: LogLevel.trace,
        sinks: <LogSink>[a, b],
      );

      logger.info('hello');

      expect(a.records, hasLength(1));
      expect(b.records, hasLength(1));
      expect(a.records.single.message, 'hello');
      expect(a.records.single.level, LogLevel.info);
    });

    test('drops records below the configured floor before building them', () {
      final sink = _CapturingSink();
      final logger = AppLogger(
        minLevel: LogLevel.warning,
        sinks: <LogSink>[sink],
      );

      logger.trace('t');
      logger.debug('d');
      logger.info('i');
      logger.warning('w');
      logger.error('e');

      expect(sink.records.map((LogRecord r) => r.level), <LogLevel>[
        LogLevel.warning,
        LogLevel.error,
      ]);
      expect(logger.isEnabled(LogLevel.debug), isFalse);
      expect(logger.isEnabled(LogLevel.warning), isTrue);
    });

    test('scoped() tags records with the category and shares sinks/floor', () {
      final sink = _CapturingSink();
      final root = AppLogger(minLevel: LogLevel.debug, sinks: <LogSink>[sink]);

      root
          .scoped('auth')
          .warning('x', fields: <String, Object?>{'status': 401});

      final LogRecord r = sink.records.single;
      expect(r.category, 'auth');
      expect(r.fields['status'], 401);
    });

    test('carries error and stackTrace through to the sink', () {
      final sink = _CapturingSink();
      final logger = AppLogger(
        minLevel: LogLevel.trace,
        sinks: <LogSink>[sink],
      );
      final err = StateError('nope');
      final st = StackTrace.current;

      logger.error('failed', error: err, stackTrace: st);

      expect(sink.records.single.error, same(err));
      expect(sink.records.single.stackTrace, same(st));
    });

    test('a throwing sink never breaks other sinks or the caller', () {
      final good = _CapturingSink();
      final logger = AppLogger(
        minLevel: LogLevel.trace,
        sinks: <LogSink>[_ThrowingSink(), good],
      );

      expect(() => logger.info('still fine'), returnsNormally);
      expect(good.records, hasLength(1));
    });
  });

  group('LogConfig', () {
    test('defaults to debug-or-warning with console on', () {
      // No --dart-define in the test runner → debug build → debug floor.
      final cfg = LogConfig.fromEnvironment();
      expect(cfg.minLevel, LogLevel.debug);
      expect(cfg.console, isTrue);
    });
  });
}
