import 'dart:async';
import 'dart:typed_data';

import 'package:connectivity_plus/connectivity_plus.dart';
import 'package:flutter/widgets.dart';
import 'package:secure_player/core/logging/logging.dart';
import 'package:secure_player/core/net/api_client.dart';
import 'package:secure_player/core/net/app_config.dart';
import 'package:secure_player/core/net/token_refresher.dart';
import 'package:secure_player/core/platform/app_platform.dart';
import 'package:secure_player/core/playback/connectivity_checker.dart';
import 'package:secure_player/core/playback/hls_key_loader.dart';
import 'package:secure_player/core/playback/local_manifest_proxy.dart';
import 'package:secure_player/core/playback/video_engine.dart';
import 'package:secure_player/core/secure_surface/secure_surface.dart';
import 'package:secure_player/core/storage/session.dart';
import 'package:secure_player/core/storage/session_store.dart';
import 'package:secure_player/data/dtos/playback_handoff.dart';
import 'package:secure_player/data/dtos/playback_manifest.dart';
import 'package:secure_player/data/playback_repository.dart';

/// A [SessionStore] backed by memory only — the test path never touches the OS
/// keystore. Doubles as a **no-write storage spy**: [writeCount] proves the key
/// loader / proxy never persist anything through it (`NFR-APP-SEC-005`).
class FakeSessionStore implements SessionStore {
  FakeSessionStore({Session? session}) : _cached = session;

  Session? _cached;
  String? _student;
  bool persist = true;
  int writeCount = 0;

  @override
  Session? get current => _cached;

  @override
  Future<Session?> load() async => _cached;

  @override
  Future<void> save(Session session, {bool? persist}) async {
    if (persist != null) this.persist = persist;
    _cached = session;
    writeCount++;
  }

  @override
  Future<void> clear() async {
    _cached = null;
    _student = null;
    persist = true;
  }

  @override
  Future<void> saveStudent(String json) async {
    _student = json;
    writeCount++;
  }

  @override
  Future<String?> loadStudent() async => _student;
}

/// A [TokenRefresher] that never succeeds — the happy-path playback tests use a
/// valid token, so refresh is never exercised.
class FakeTokenRefresher implements TokenRefresher {
  @override
  Future<Session?> refresh(String refreshToken) async => null;
}

/// A capturing [LogSink] — lets a test assert that **no** secret (the AES key,
/// signed URLs, the handoff, the manifest body) ever reaches a log
/// (`NFR-APP-SEC-003` / `NFR-APP-SEC-005`).
class CapturingLogSink implements LogSink {
  final List<LogRecord> records = <LogRecord>[];

  @override
  void emit(LogRecord record) => records.add(record);

  /// Every emitted message + field value flattened to a single searchable blob.
  String get blob {
    final StringBuffer b = StringBuffer();
    for (final LogRecord r in records) {
      b.writeln(r.message);
      r.fields.forEach((String k, Object? v) => b.writeln('$k=$v'));
      if (r.error != null) b.writeln(r.error);
    }
    return b.toString();
  }
}

/// A logger whose every record lands in [sink]. Captures all levels.
AppLogger capturingLogger(CapturingLogSink sink) =>
    AppLogger(minLevel: LogLevel.trace, sinks: <LogSink>[sink]);

/// A session with a known access token, valid well into the future.
Session validSession(String accessToken) => Session(
  accessToken: accessToken,
  refreshToken: 'refresh-token',
  accessTokenExpiresAt: DateTime.now().toUtc().add(const Duration(minutes: 15)),
  refreshTokenExpiresAt: DateTime.now().toUtc().add(const Duration(days: 7)),
);

/// Builds a real [ApiClient] aimed at [baseUrl] with [store]'s token, so a test
/// exercises the genuine Bearer-injection + header path (contract §G).
ApiClient realApiClient({
  required String baseUrl,
  required SessionStore store,
}) => ApiClient(
  config: AppConfig(
    apiBaseUrl: baseUrl,
    appVersion: '1.0.0',
    portalUrl: 'https://portal.test',
  ),
  store: store,
  refresher: FakeTokenRefresher(),
  platform: AppPlatform(target: AppTarget.windows),
);

/// A hand fake [PlaybackRepository] — the gate, scripted. Records call counts so
/// a test can assert that an in-TTL retry does **not** re-redeem / re-Play
/// (`FR-APP-AUTH-003`).
class FakePlaybackRepository implements PlaybackRepository {
  FakePlaybackRepository({this.manifest, this.redeemError, Uint8List? key})
    : _key = key ?? fixtureKeyBytes();

  final PlaybackManifest? manifest;
  final Object? redeemError;
  final Uint8List _key;

  int redeemCalls = 0;
  int startPlaybackCalls = 0;
  int keyCalls = 0;

  @override
  Future<PlaybackManifest> redeem(String handoffCode) async {
    redeemCalls++;
    if (redeemError != null) throw redeemError!;
    return manifest ?? fixtureManifest();
  }

  @override
  Future<PlaybackHandoff> startPlayback(String videoId) async {
    startPlaybackCalls++;
    return PlaybackHandoff(
      handoffCode: 'deadbeef',
      expiresAtUtc: DateTime.now().toUtc().add(const Duration(seconds: 60)),
    );
  }

  @override
  Future<Uint8List> keyBytes(String videoId) async {
    keyCalls++;
    return _key;
  }
}

/// A hand fake [VideoEngine] — no native libmpv (`NFR-APP-REL-003`). Records
/// control calls and lets a test drive the observation streams.
class FakeVideoEngine implements VideoEngine {
  final List<Uri> opened = <Uri>[];
  int playCalls = 0;
  int pauseCalls = 0;
  double? lastRate;
  double? lastVolume;
  Duration? lastSeek;
  bool disposed = false;

  final StreamController<bool> _playing = StreamController<bool>.broadcast();
  final StreamController<Duration> _position =
      StreamController<Duration>.broadcast();
  final StreamController<Duration> _duration =
      StreamController<Duration>.broadcast();
  final StreamController<Duration> _buffer =
      StreamController<Duration>.broadcast();
  final StreamController<bool> _buffering = StreamController<bool>.broadcast();
  final StreamController<bool> _completed = StreamController<bool>.broadcast();
  final StreamController<String> _error = StreamController<String>.broadcast();

  void emitPlaying(bool v) => _playing.add(v);
  void emitPosition(Duration v) => _position.add(v);
  void emitDuration(Duration v) => _duration.add(v);
  void emitBuffer(Duration v) => _buffer.add(v);
  void emitBuffering(bool v) => _buffering.add(v);
  void emitCompleted(bool v) => _completed.add(v);
  void emitError(String v) => _error.add(v);

  @override
  Future<void> open(Uri url, {bool autoPlay = true}) async => opened.add(url);

  @override
  Future<void> play() async => playCalls++;

  @override
  Future<void> pause() async => pauseCalls++;

  @override
  Future<void> seek(Duration position) async => lastSeek = position;

  @override
  Future<void> setRate(double rate) async => lastRate = rate;

  @override
  Future<void> setVolume(double volume) async => lastVolume = volume;

  @override
  Stream<bool> get playingStream => _playing.stream;
  @override
  Stream<Duration> get positionStream => _position.stream;
  @override
  Stream<Duration> get durationStream => _duration.stream;
  @override
  Stream<Duration> get bufferStream => _buffer.stream;
  @override
  Stream<bool> get bufferingStream => _buffering.stream;
  @override
  Stream<bool> get completedStream => _completed.stream;
  @override
  Stream<String> get errorStream => _error.stream;

  @override
  Widget buildSurface() => const SizedBox.shrink();

  @override
  Future<void> dispose() async {
    disposed = true;
    await _playing.close();
    await _position.close();
    await _duration.close();
    await _buffer.close();
    await _buffering.close();
    await _completed.close();
    await _error.close();
  }
}

/// Sixteen deterministic AES-128 key bytes for fixtures.
Uint8List fixtureKeyBytes() =>
    Uint8List.fromList(List<int>.generate(16, (int i) => i + 1));

/// A redeemed manifest fixture with a known body + key URL, valid for [ttl].
/// [accessRemaining]/[accessAllowed] are the "N of M views left" budget
/// (contract §D, FR-APP-VID-004) — default "2 of 3".
PlaybackManifest fixtureManifest({
  Duration ttl = const Duration(seconds: 120),
  String keyUrl = 'https://api.test/api/me/videos/vid-1/hls.key',
  int? accessRemaining = 2,
  int? accessAllowed = 3,
  String? videoTitle = 'Quadratic equations',
  String? watermark = 'STU-7K2M9X · Layla Ahmed',
}) {
  final String body = <String>[
    '#EXTM3U',
    '#EXT-X-VERSION:3',
    '#EXT-X-KEY:METHOD=AES-128,URI="$keyUrl",IV=0x0',
    '#EXTINF:6.0,',
    'https://r2.test/seg0.ts?sig=SIGNED',
    '#EXT-X-ENDLIST',
  ].join('\n');
  return PlaybackManifest(
    manifestContent: body,
    keyUrl: keyUrl,
    expiresAtUtc: DateTime.now().toUtc().add(ttl),
    accessRemaining: accessRemaining,
    accessAllowed: accessAllowed,
    videoTitle: videoTitle,
    watermark: watermark,
  );
}

/// Asserts a blob of text contains none of [secrets]. Returns the first leaked
/// secret, or `null` when clean.
String? firstLeak(String haystack, List<String> secrets) {
  for (final String s in secrets) {
    if (s.isNotEmpty && haystack.contains(s)) return s;
  }
  return null;
}

/// A [ConnectivityChecker] that never touches native method channels.
/// Defaults to wifi (online). Call [push] to drive connectivity changes;
/// set [current] before a [check] call to control the synchronous result.
class FakeConnectivityChecker implements ConnectivityChecker {
  List<ConnectivityResult> current = <ConnectivityResult>[
    ConnectivityResult.wifi,
  ];
  final StreamController<List<ConnectivityResult>> _ctrl =
      StreamController<List<ConnectivityResult>>.broadcast();

  @override
  Future<List<ConnectivityResult>> check() async => current;

  @override
  Stream<List<ConnectivityResult>> get onChange => _ctrl.stream;

  void push(List<ConnectivityResult> results) {
    current = results;
    _ctrl.add(results);
  }

  Future<void> dispose() => _ctrl.close();
}

/// A hand fake [SecureSurface] — no native channel (`NFR-APP-REL-003`). Scripted
/// to return [enableStatus]; records `enable`/`disable` call counts so a test
/// can prove `enable == 1` on mount and `disable` on dispose (`FR-APP-CAP-003`,
/// `NFR-APP-CAP-006`). [captureController] lets an iOS test push capture events.
class FakeSecureSurface implements SecureSurface {
  FakeSecureSurface({this.enableStatus = SecureSurfaceStatus.protected});

  SecureSurfaceStatus enableStatus;
  int enableCalls = 0;
  int disableCalls = 0;

  final StreamController<SecureSurfaceEvent> captureController =
      StreamController<SecureSurfaceEvent>.broadcast();

  @override
  Future<SecureSurfaceStatus> enable() async {
    enableCalls++;
    return enableStatus;
  }

  @override
  Future<void> disable() async => disableCalls++;

  @override
  Stream<SecureSurfaceEvent> get captureEvents => captureController.stream;

  Future<void> dispose() => captureController.close();
}

/// A [LocalManifestProxy] that **never binds a real `HttpServer`** — so a
/// `testWidgets` (fake-async) page test doesn't trip the pending-timer guard
/// over the server's idle timer. Returns a fixed loopback URL.
class FakeLocalManifestProxy extends LocalManifestProxy {
  FakeLocalManifestProxy() : super(HlsKeyLoader(FakePlaybackRepository()));

  final Uri started = Uri.parse('http://127.0.0.1:0/manifest.m3u8');
  int stopCalls = 0;

  @override
  Future<Uri> start({
    required PlaybackManifest manifest,
    required String videoId,
  }) async => started;

  @override
  Future<void> stop() async => stopCalls++;
}
