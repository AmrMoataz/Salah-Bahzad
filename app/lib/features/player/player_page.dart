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
import '../../core/playback/video_engine.dart';
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
  @override
  void initState() {
    super.initState();
    final PendingDeepLink? pending = ref.read(pendingDeepLinkProvider);
    final PlaybackRequest? request = pending is PendingValid
        ? pending.request
        : null;

    WidgetsBinding.instance.addPostFrameCallback((_) {
      // Release the pending link so the router stops redirecting here.
      ref.read(pendingDeepLinkProvider.notifier).consume();
      if (request == null) {
        // No lesson to play (cold open / stale nav) — return to home.
        if (mounted) context.go('/idle');
        return;
      }
      // A2 will toggle the OS capture black-out here (no-op in A1).
      unawaited(ref.read(playerControllerProvider.notifier).start(request));
    });
  }

  @override
  void dispose() {
    // Make sure we never leave the OS in fullscreen after the player closes.
    unawaited(_applyFullscreen(false));
    super.dispose();
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
    unawaited(
      _applyFullscreen(ref.read(playerControllerProvider).fullscreen),
    );
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

    // Watermark identity (serial · fullName). While the profile loads, fall
    // back to the signed-in name so the overlay is never blank.
    final AuthState auth = ref.watch(authControllerProvider);
    final String fallbackName = auth is AuthActive ? auth.student.fullName : '';
    final String watermarkLabel = ref
        .watch(studentProfileProvider)
        .maybeWhen(
          data: (profile) {
            // Until the backend stream populates `serial` it's empty → strip
            // the leading "· " so the fallback is just the name.
            return profile.watermarkLabel.replaceFirst(
              RegExp(r'^\s*·\s*'),
              '',
            );
          },
          orElse: () => fallbackName,
        );

    return Scaffold(
      backgroundColor: SbColors.playerBg,
      body: PlayerView(
        state: state,
        lessonTitle: 'Secure lesson',
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
