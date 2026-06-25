import 'package:flutter/widgets.dart';
import 'package:media_kit/media_kit.dart';
import 'package:media_kit_video/media_kit_video.dart';

import '../theme/sb_tokens.dart';
import 'video_engine.dart';

/// The production [VideoEngine] — media_kit / libmpv. libmpv decrypts AES-128
/// HLS natively on all four targets and fetches the `#EXT-X-KEY` URI over HTTP;
/// the manifest we feed it points that URI at the in-process loopback proxy
/// (`LocalManifestProxy`), which attaches the Bearer — so the key/segment bytes
/// and the AES key never need a header on the engine itself and never touch disk
/// (`NFR-APP-SEC-005`).
///
/// `MediaKit.ensureInitialized()` must have run once at startup (see
/// `main.dart`). ABR is left to libmpv (up to 1080p, `FR-APP-VID-005`).
class MediaKitVideoEngine implements VideoEngine {
  MediaKitVideoEngine() : _player = Player() {
    _controller = VideoController(_player);
  }

  final Player _player;
  late final VideoController _controller;

  @override
  Future<void> open(Uri url, {bool autoPlay = true}) =>
      _player.open(Media(url.toString()), play: autoPlay);

  @override
  Future<void> play() => _player.play();

  @override
  Future<void> pause() => _player.pause();

  @override
  Future<void> seek(Duration position) => _player.seek(position);

  @override
  Future<void> setRate(double rate) => _player.setRate(rate);

  @override
  Future<void> setVolume(double volume) => _player.setVolume(volume);

  @override
  Stream<bool> get playingStream => _player.stream.playing;

  @override
  Stream<Duration> get positionStream => _player.stream.position;

  @override
  Stream<Duration> get durationStream => _player.stream.duration;

  @override
  Stream<Duration> get bufferStream => _player.stream.buffer;

  @override
  Stream<bool> get bufferingStream => _player.stream.buffering;

  @override
  Stream<bool> get completedStream => _player.stream.completed;

  @override
  Stream<String> get errorStream => _player.stream.error;

  @override
  Widget buildSurface() => Video(
    controller: _controller,
    fit: BoxFit.contain,
    fill: SbColors.playerDeep,
    controls: NoVideoControls,
  );

  @override
  Future<void> dispose() => _player.dispose();
}
