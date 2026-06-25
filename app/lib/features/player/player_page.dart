import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:url_launcher/url_launcher.dart';
import 'package:window_manager/window_manager.dart';

import '../../app/providers.dart';
import '../../core/deeplink/pending_deep_link.dart';
import '../../core/deeplink/playback_request.dart';
import '../../core/platform/app_platform.dart';
import '../../core/playback/video_engine.dart';
import '../../core/secure_surface/secure_surface.dart';
import '../../core/theme/sb_tokens.dart';
import '../auth/auth_state.dart';
import 'player_controller.dart';
import 'player_state.dart';
import 'player_view.dart';

/// The live Player route (replaces the A0 placeholder). Owns the engine
/// lifecycle, drives `redeem → key → play` via [playerControllerProvider], and
/// hosts the (no-op in A1) capture-flag hook — A2's `core/secure_surface` fills
/// the native side. Releases the consumed deep link after the first frame
/// (same pattern the placeholder used).
class PlayerPage extends ConsumerStatefulWidget {
  const PlayerPage({super.key});

  @override
  ConsumerState<PlayerPage> createState() => _PlayerPageState();
}

class _PlayerPageState extends ConsumerState<PlayerPage> {
  /// iOS-only `isCaptured`/screenshot stream (F5); an empty no-op off iOS.
  StreamSubscription<SecureSurfaceEvent>? _captureSub;

  // Captured in [initState] so [dispose] uses the held instances — `ref.read`
  // is unreliable once the element is torn down (mirrors how
  // `PlayerController._teardown` holds its engine/proxy).
  late final SecureSurface _secure;
  late final SecureSurfaceStatusController _statusController;
  late final AppTarget _target;

  @override
  void initState() {
    super.initState();
    _secure = ref.read(secureSurfaceProvider);
    _statusController = ref.read(secureSurfaceStatusProvider.notifier);
    _target = ref.read(appPlatformProvider).target;

    final PendingDeepLink? pending = ref.read(pendingDeepLinkProvider);
    final PlaybackRequest? request = pending is PendingValid
        ? pending.request
        : null;

    // ON before the first frame (`NFR-APP-CAP-006`/`FR-APP-CAP-003`): engage the
    // OS capture black-out synchronously from initState, BEFORE the engine
    // opens. We `await enable()` so playback never starts before protection is
    // active; on an unsupported platform we REFUSE (`NFR-APP-COMPAT-002`)
    // instead of opening the engine unprotected. (Replaces the A1 stub here.)
    unawaited(_engageProtectionThenStart(request));
  }

  Future<void> _engageProtectionThenStart(PlaybackRequest? request) async {
    SecureSurfaceStatus status;
    try {
      status = await _secure.enable();
    } catch (_) {
      // The facade never throws into the player, but stay fail-safe.
      status = SecureSurfaceStatus.unsupported;
    }
    if (!mounted) return;
    _statusController.set(status);

    // iOS reactive blank/pause (`FR-APP-CAP-002`/`NFR-APP-CAP-004`) — page-only,
    // never on the pure view. Off iOS the stream is empty so this is a no-op.
    if (_target == AppTarget.ios) {
      _captureSub = _secure.captureEvents.listen(_onCaptureEvent);
    }

    // Release the pending link so the router stops redirecting here.
    ref.read(pendingDeepLinkProvider.notifier).consume();
    if (request == null) {
      // No lesson to play (cold open / stale nav) — return to home.
      if (mounted) context.go('/idle');
      return;
    }

    // COMPAT-002 warn + REFUSE: where the black-out is *required* but can't be
    // guaranteed (desktop/Android reporting unsupported), do NOT open the engine
    // — surface a refusal state. iOS is the one best-effort exception: it plays
    // with the amber banner + watermark (`NFR-APP-CAP-005`), never refused.
    final bool refuse =
        status == SecureSurfaceStatus.unsupported && _target != AppTarget.ios;
    if (refuse) {
      ref.read(playerControllerProvider.notifier).refuseUnprotected();
      return;
    }

    unawaited(ref.read(playerControllerProvider.notifier).start(request));
  }

  void _onCaptureEvent(SecureSurfaceEvent event) {
    final PlayerController controller = ref.read(
      playerControllerProvider.notifier,
    );
    switch (event) {
      case SecureSurfaceEvent.captureStarted:
        unawaited(controller.onCaptureStarted());
      case SecureSurfaceEvent.captureStopped:
        unawaited(controller.onCaptureStopped());
      case SecureSurfaceEvent.screenshotTaken:
        // Accepted gap (`NFR-APP-CAP-005`): flag only, never block — the
        // watermark is the deterrent. No PII/handoff is logged.
        ref
            .read(loggerProvider)
            .scoped('secure_surface')
            .info(
              'iOS screenshot detected (accepted gap; watermark relied on)',
            );
    }
  }

  @override
  void dispose() {
    unawaited(_captureSub?.cancel());
    // OFF on leaving the player (`FR-APP-CAP-003`): release the black-out beside
    // the existing fullscreen reset, wrapped crash-proof so teardown never
    // throws.
    _disableSecureSurface();
    // Make sure we never leave the OS in fullscreen after the player closes.
    unawaited(_applyFullscreen(false));
    super.dispose();
  }

  void _disableSecureSurface() {
    try {
      // The native release is the load-bearing "off on leave". We do NOT reset
      // the status notifier here: notifying it would `markNeedsBuild` the
      // already-defunct page element. The next player mount sets the status
      // afresh in initState, so a stale value is never observed.
      unawaited(_secure.disable());
    } catch (_) {
      // Releasing protection is best-effort — never crash the teardown over it.
    }
  }

  Future<void> _applyFullscreen(bool on) async {
    try {
      if (ref.read(appPlatformProvider).isDesktop) {
        await windowManager.setFullScreen(on);
      } else {
        await SystemChrome.setEnabledSystemUIMode(
          on ? SystemUiMode.immersiveSticky : SystemUiMode.edgeToEdge,
        );
      }
    } catch (_) {
      // Fullscreen is a nicety — never crash the player over it.
    }
  }

  void _onToggleFullscreen() {
    ref.read(playerControllerProvider.notifier).toggleFullscreen();
    unawaited(_applyFullscreen(ref.read(playerControllerProvider).fullscreen));
  }

  Future<void> _onPrimaryAction(PlayerError error) async {
    switch (error.primaryAction) {
      case PlayerAction.retry:
        await ref.read(playerControllerProvider.notifier).retry();
      case PlayerAction.signIn:
        if (mounted) context.go('/signin');
      case PlayerAction.openPortal:
        await _openPortal();
      case PlayerAction.backToPortal:
        if (mounted) context.go('/idle');
    }
  }

  Future<void> _openPortal() async {
    final Uri? uri = Uri.tryParse(ref.read(appConfigProvider).portalUrl);
    if (uri == null) return;
    try {
      await launchUrl(uri, mode: LaunchMode.externalApplication);
    } catch (_) {
      // Opening the portal must never crash the app.
    }
  }

  @override
  Widget build(BuildContext context) {
    final PlayerState state = ref.watch(playerControllerProvider);
    final PlayerController controller = ref.read(
      playerControllerProvider.notifier,
    );
    final VideoEngine engine = ref.watch(videoEngineProvider);

    // Watermark identity "serial · fullName" (FR-APP-VID-003). Primary source is
    // the redeem manifest (`state.watermark`) — carried per-playback, so the
    // serial is always present once playback starts and never depends on a
    // separate fetch. Until the manifest arrives we fall back to the profile
    // read, then to the signed-in name, so the overlay is never blank.
    final AuthState auth = ref.watch(authControllerProvider);
    final String fallbackName = auth is AuthActive ? auth.student.fullName : '';
    final String profileWatermark = ref
        .watch(studentProfileProvider)
        .maybeWhen(
          // Strip a leading "· " in the (rare) case the profile serial is empty.
          data: (profile) =>
              profile.watermarkLabel.replaceFirst(RegExp(r'^\s*·\s*'), ''),
          orElse: () => fallbackName,
        );
    final String? manifestWatermark = state.watermark;
    final String watermarkLabel =
        (manifestWatermark != null && manifestWatermark.isNotEmpty)
        ? manifestWatermark
        : profileWatermark;

    // The video's own title (from the manifest); a neutral label until it loads.
    final String lessonTitle =
        (state.videoTitle != null && state.videoTitle!.isNotEmpty)
        ? state.videoTitle!
        : 'Secure lesson';

    // The banner reflects the REAL protection state (F6): green when protected,
    // amber when best-effort/unsupported (iOS / unguaranteed), hidden when off.
    final SecureSurfaceStatus captureStatus = ref.watch(
      secureSurfaceStatusProvider,
    );

    return Scaffold(
      backgroundColor: SbColors.playerBg,
      body: PlayerView(
        state: state,
        captureStatus: captureStatus,
        lessonTitle: lessonTitle,
        watermarkLabel: watermarkLabel,
        videoSurface: state.hasError
            ? const ColoredBox(color: SbColors.playerBg)
            : engine.buildSurface(),
        onBack: () => context.go('/idle'),
        onPlayPause: controller.togglePlay,
        onSeek: controller.seek,
        onCycleSpeed: controller.cycleSpeed,
        onToggleMute: controller.toggleMute,
        onToggleFullscreen: _onToggleFullscreen,
        onPrimaryAction: () {
          final PlayerError? error = state.error;
          if (error != null) unawaited(_onPrimaryAction(error));
        },
      ),
    );
  }
}
