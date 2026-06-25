import 'dart:async';

import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../app/providers.dart';
import '../../core/deeplink/playback_request.dart';
import '../../core/logging/logging.dart';
import '../../core/net/api_exception.dart';
import '../../core/playback/local_manifest_proxy.dart';
import '../../core/playback/video_engine.dart';
import '../../data/dtos/playback_manifest.dart';
import '../../data/playback_repository.dart';
import 'player_state.dart';

/// Drives the secure player: **redeem (D2) → feed the rewritten manifest to the
/// engine via the loopback proxy → play**. The deep-link path **never** calls D1
/// (`startPlayback`) — the portal already minted + spent the view; calling it
/// here would double-decrement (`FR-APP-AUTH-003`).
///
/// Retry rule (§D.3): within the handoff/manifest TTL a retry **reuses the same
/// manifest** (re-opens the engine) — it never re-redeems and never re-Plays, so
/// no second view is spent. Once the TTL has elapsed (or `410 handoff_expired`)
/// the player surfaces "press Play again" guidance instead of silently re-minting
/// (master §10.1).
///
/// The engine is the clock: position/duration/buffer come from its streams, not
/// a wall-clock poll — so playback survives resize/rotation (`NFR-APP-REL-001`).
class PlayerController extends Notifier<PlayerState> {
  // Captured in [build] so [_teardown] (which runs during disposal, where
  // `ref.read` is illegal) uses the held instances. The engine + proxy are
  // **watched** (not read) so the controller keeps those auto-disposed providers
  // alive for its lifetime and they tear down with it on leaving the route.
  late PlaybackRepository _repo;
  late LocalManifestProxy _proxy;
  late VideoEngine _engine;
  late AppLogger _log;

  PlaybackRequest? _request;
  PlaybackManifest? _manifest;
  Uri? _localUrl;
  bool _started = false;
  final List<StreamSubscription<Object?>> _subs =
      <StreamSubscription<Object?>>[];

  @override
  PlayerState build() {
    _repo = ref.read(playbackRepositoryProvider);
    _proxy = ref.watch(localManifestProxyProvider);
    _engine = ref.watch(videoEngineProvider);
    _log = ref.read(loggerProvider).scoped('player');
    ref.onDispose(_teardown);
    return const PlayerState();
  }

  /// Entry point from the page (on mount). Redeems the handoff and starts
  /// playback. Guarded so a rebuild can't double-start.
  Future<void> start(PlaybackRequest request) async {
    if (_started) return;
    _started = true;
    _request = request;
    _bindEngine();
    await _redeemAndPlay();
  }

  Future<void> _redeemAndPlay() async {
    final PlaybackRequest? request = _request;
    if (request == null) return;
    state = state.copyWith(status: PlayerStatus.loading, clearError: true);
    try {
      final PlaybackManifest manifest = await _repo.redeem(request.handoff);
      _manifest = manifest;
      // Surface the per-playback manifest extras (contract §D): "N of M views
      // left" (FR-APP-VID-004; gate already spent this view, so accessRemaining
      // is the post-Play count), the video's own title (top bar), and the bound
      // student's serial·name watermark (FR-APP-VID-003). Null (older API) keeps
      // the fallbacks. Set BEFORE opening the engine so they show on first frame.
      state = state.copyWith(
        viewsLeft: manifest.accessRemaining,
        viewsTotal: manifest.accessAllowed,
        videoTitle: manifest.videoTitle,
        watermark: manifest.watermark,
      );
      await _openManifest(manifest, request.videoId);
    } on ApiException catch (e) {
      _fail(PlayerError.fromApi(e));
    } catch (e, s) {
      _log.warning('Unexpected redeem failure', error: e, stackTrace: s);
      _fail(_engineError());
    }
  }

  Future<void> _openManifest(PlaybackManifest manifest, String videoId) async {
    _localUrl = await _proxy.start(manifest: manifest, videoId: videoId);
    await _engine.open(_localUrl!, autoPlay: true);
  }

  /// Re-attempt after a transient failure. **Reuses the same handoff/manifest**
  /// within TTL (no second view spent):
  ///  * manifest already redeemed + still valid → re-open the engine only;
  ///  * redeem hadn't succeeded yet but the handoff is still valid → re-redeem
  ///    the **same** handoff (D2 spends no view);
  ///  * TTL elapsed → "press Play again" (no silent re-mint).
  Future<void> retry() async {
    final DateTime now = DateTime.now().toUtc();
    final PlaybackManifest? manifest = _manifest;
    final String? videoId = _request?.videoId;

    if (manifest != null && videoId != null && !manifest.isExpiredAt(now)) {
      state = state.copyWith(status: PlayerStatus.loading, clearError: true);
      try {
        await _openManifest(manifest, videoId);
      } catch (e, s) {
        _log.warning('Retry re-open failed', error: e, stackTrace: s);
        _fail(_engineError());
      }
      return;
    }

    if (manifest != null && manifest.isExpiredAt(now)) {
      // The signed URLs / handoff expired — do not silently re-mint.
      _fail(_handoffExpired());
      return;
    }

    // Redeem never completed; reuse the SAME handoff while it is still valid.
    await _redeemAndPlay();
  }

  // ── Capture protection (A2) ──────────────────────────────────────────────────

  /// COMPAT-002 fail-safe (`NFR-APP-COMPAT-002`): the OS cannot **guarantee**
  /// the capture black-out on this device (e.g. Windows < 2004), so the player
  /// **refuses** protected playback rather than open the engine unprotected.
  /// Surfaces a player-reachable refusal state — **no engine opened, no view
  /// spent** (the page must not call [start] in this branch). iOS is the one
  /// best-effort exception and is **not** routed here (it plays with the amber
  /// banner + watermark).
  void refuseUnprotected() {
    _started = true; // belt-and-suspenders: never start after a refusal.
    _fail(_captureUnsupported());
  }

  /// iOS reactive capture defence (`FR-APP-CAP-002` / `NFR-APP-CAP-004`): active
  /// screen capture / mirroring started → pause so the recording captures the
  /// paused frame, not the lesson. The watermark stays on screen.
  Future<void> onCaptureStarted() async {
    if (state.isPlaying) await _engine.pause();
  }

  /// Capture / mirroring stopped → resume (only from a clean paused state, never
  /// out of an error/ended state).
  Future<void> onCaptureStopped() async {
    if (state.status == PlayerStatus.paused) await _engine.play();
  }

  // ── Controls ────────────────────────────────────────────────────────────────

  Future<void> togglePlay() async {
    if (state.isPlaying) {
      await _engine.pause();
    } else {
      await _engine.play();
    }
  }

  Future<void> seek(Duration position) async {
    final Duration clamped = position < Duration.zero
        ? Duration.zero
        : (position > state.duration ? state.duration : position);
    state = state.copyWith(position: clamped);
    await _engine.seek(clamped);
  }

  /// Cycles 1× → 1.25× → 1.5× → 2× → 1× (`FR-APP-VID-005`).
  Future<void> cycleSpeed() async {
    final int idx = kPlaybackSpeeds.indexOf(state.speed);
    final double next = kPlaybackSpeeds[(idx + 1) % kPlaybackSpeeds.length];
    state = state.copyWith(speed: next);
    await _engine.setRate(next);
  }

  Future<void> toggleMute() async {
    final bool muted = !state.muted;
    state = state.copyWith(muted: muted);
    await _engine.setVolume(muted ? 0 : state.volume * 100);
  }

  Future<void> setVolume(double volume) async {
    final double v = volume.clamp(0.0, 1.0);
    state = state.copyWith(volume: v, muted: v == 0);
    await _engine.setVolume(v * 100);
  }

  void toggleFullscreen() =>
      state = state.copyWith(fullscreen: !state.fullscreen);

  // ── Engine binding ────────────────────────────────────────────────────────

  void _bindEngine() {
    _subs.add(
      _engine.playingStream.listen((bool playing) {
        if (state.status == PlayerStatus.error ||
            state.status == PlayerStatus.ended) {
          return;
        }
        state = state.copyWith(
          status: playing ? PlayerStatus.playing : PlayerStatus.paused,
        );
      }),
    );
    _subs.add(
      _engine.positionStream.listen(
        (Duration p) => state = state.copyWith(position: p),
      ),
    );
    _subs.add(
      _engine.durationStream.listen(
        (Duration d) => state = state.copyWith(duration: d),
      ),
    );
    _subs.add(
      _engine.bufferStream.listen(
        (Duration b) => state = state.copyWith(buffered: b),
      ),
    );
    _subs.add(
      _engine.bufferingStream.listen(
        (bool b) => state = state.copyWith(buffering: b),
      ),
    );
    _subs.add(
      _engine.completedStream.listen((bool done) {
        if (done) state = state.copyWith(status: PlayerStatus.ended);
      }),
    );
    _subs.add(
      _engine.errorStream.listen((String _) {
        // libmpv error strings can echo the loopback URL — never log the body.
        _log.warning('Engine reported a playback error');
        _fail(_engineError());
      }),
    );
  }

  void _fail(PlayerError error) =>
      state = state.copyWith(status: PlayerStatus.error, error: error);

  PlayerError _engineError() => const PlayerError(
    kind: PlayerErrorKind.server,
    title: 'Something went wrong',
    message: "That one's on us, not you. Give it another go in a moment.",
    primaryActionLabel: 'Try again',
    primaryAction: PlayerAction.retry,
  );

  PlayerError _captureUnsupported() => const PlayerError(
    kind: PlayerErrorKind.forbidden,
    title: "We can't protect this lesson here",
    message:
        "This device can't block screen capture for this video, so playback is "
        'turned off to keep the lesson safe. Try the app on a newer device or '
        'an updated OS.',
    primaryActionLabel: 'Back to portal',
    primaryAction: PlayerAction.backToPortal,
    reason: 'capture_unsupported',
  );

  PlayerError _handoffExpired() => const PlayerError(
    kind: PlayerErrorKind.notfound,
    title: "We can't find this lesson",
    message:
        'This play link expired. Press Play again in the portal to start '
        'a fresh session.',
    primaryActionLabel: 'Back to portal',
    primaryAction: PlayerAction.backToPortal,
    reason: 'handoff_expired',
  );

  Future<void> _teardown() async {
    for (final StreamSubscription<Object?> s in _subs) {
      await s.cancel();
    }
    _subs.clear();
    await _proxy.stop();
    _manifest = null;
    _localUrl = null;
  }
}
