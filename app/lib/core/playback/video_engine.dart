import 'package:flutter/widgets.dart';

/// The video-engine seam. The `PlayerController` drives playback through this
/// interface and the `PlayerPage` renders [buildSurface] — so the controller is
/// unit-tested against a hand fake and `flutter test` **never** touches the
/// native engine (`NFR-APP-REL-003`). The production impl is
/// [MediaKitVideoEngine] (libmpv); it decrypts AES-128 HLS natively and fetches
/// the key from the in-process loopback proxy (`LocalManifestProxy`).
abstract class VideoEngine {
  /// Opens [url] (the loopback manifest URL) and, when [autoPlay], starts.
  Future<void> open(Uri url, {bool autoPlay = true});

  Future<void> play();
  Future<void> pause();
  Future<void> seek(Duration position);

  /// Playback speed (1.0 / 1.25 / 1.5 / 2.0).
  Future<void> setRate(double rate);

  /// Volume on a 0–100 scale (libmpv's range).
  Future<void> setVolume(double volume);

  // ── Observation streams (the engine is the clock — never a wall poll) ──────
  Stream<bool> get playingStream;
  Stream<Duration> get positionStream;
  Stream<Duration> get durationStream;
  Stream<Duration> get bufferStream;
  Stream<bool> get bufferingStream;
  Stream<bool> get completedStream;

  /// libmpv surfaces a load/decrypt failure as an error string here.
  Stream<String> get errorStream;

  /// The video surface widget (the `PlayerPage` hosts it inside the stage).
  Widget buildSurface();

  Future<void> dispose();
}
