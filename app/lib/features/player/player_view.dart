import 'package:flutter/material.dart';

import '../../core/responsive/breakpoints.dart';
import '../../core/responsive/responsive_builder.dart';
import '../../core/theme/sb_assets.dart';
import '../../core/theme/sb_text.dart';
import '../../core/theme/sb_tokens.dart';
import '../../widgets/encrypted_chip.dart';
import 'player_state.dart';
import 'widgets/capture_blocked_banner.dart';
import 'widgets/dynamic_watermark.dart';
import 'widgets/player_controls.dart';

/// The pure, golden-tested Player presentation (`PLAYER` banner, master §5.3):
/// dark stage · top bar (back · title · **Encrypted** chip · **"N of M views
/// left"**) · the dual-layer watermark · the static "Screen capture blocked"
/// banner · the controls bar. **No engine/timer/Firebase here** — it takes a
/// [PlayerState] + callbacks + a pre-built [videoSurface] (so goldens never
/// touch the native engine).
class PlayerView extends StatelessWidget {
  const PlayerView({
    super.key,
    required this.state,
    required this.lessonTitle,
    required this.watermarkLabel,
    required this.videoSurface,
    required this.onBack,
    required this.onPlayPause,
    required this.onSeek,
    required this.onCycleSpeed,
    required this.onToggleMute,
    required this.onToggleFullscreen,
    required this.onPrimaryAction,
    this.animateWatermark = true,
  });

  final PlayerState state;
  final String lessonTitle;
  final String watermarkLabel;

  /// The engine render surface, injected by the page; a placeholder in goldens.
  final Widget videoSurface;

  final VoidCallback onBack;
  final VoidCallback onPlayPause;
  final ValueChanged<Duration> onSeek;
  final VoidCallback onCycleSpeed;
  final VoidCallback onToggleMute;
  final VoidCallback onToggleFullscreen;

  /// The failure-state primary action (sign in / open portal / back / retry).
  final VoidCallback onPrimaryAction;

  final bool animateWatermark;

  @override
  Widget build(BuildContext context) {
    return ResponsiveBuilder(
      builder: (BuildContext context, SbLayout layout) {
        return ColoredBox(
          color: SbColors.playerBg,
          child: Stack(
            fit: StackFit.expand,
            children: <Widget>[
              // The video stage (or its dark fill) sits behind everything.
              Positioned.fill(child: videoSurface),

              // Layer 2 — the moving watermark over the frame (not over the
              // error overlay, which replaces the stage content).
              if (!state.hasError)
                Positioned.fill(
                  child: DynamicWatermark(
                    label: watermarkLabel,
                    animate: animateWatermark,
                  ),
                ),

              // Top bar: back · title · Encrypted · "N of M views left".
              Positioned(
                top: 0,
                left: 0,
                right: 0,
                child: _TopBar(
                  title: lessonTitle,
                  viewsLeft: state.viewsLeft,
                  viewsTotal: state.viewsTotal,
                  onBack: onBack,
                ),
              ),

              // Static "Screen capture blocked" reassurance (A1 banner only).
              if (!state.hasError)
                const Positioned(
                  top: 64,
                  left: 14,
                  child: CaptureBlockedBanner(),
                ),

              // Buffering / loading spinner.
              if (state.status == PlayerStatus.loading)
                const Center(child: _StageSpinner())
              else if (state.buffering && !state.hasError)
                const Center(child: _StageSpinner()),

              // Controls (hidden behind the error overlay).
              if (!state.hasError)
                Positioned(
                  bottom: 0,
                  left: 0,
                  right: 0,
                  child: PlayerControls(
                    position: state.position,
                    duration: state.duration,
                    buffered: state.buffered,
                    playing: state.isPlaying,
                    muted: state.muted,
                    fullscreen: state.fullscreen,
                    speed: state.speed,
                    compact: layout.isCompact,
                    onPlayPause: onPlayPause,
                    onSeek: onSeek,
                    onCycleSpeed: onCycleSpeed,
                    onToggleMute: onToggleMute,
                    onToggleFullscreen: onToggleFullscreen,
                  ),
                ),

              // Inline failure state (player-reachable; FR-APP-ERR-002).
              if (state.hasError)
                Positioned.fill(
                  child: _PlayerErrorOverlay(
                    error: state.error!,
                    onPrimary: onPrimaryAction,
                  ),
                ),
            ],
          ),
        );
      },
    );
  }
}

class _TopBar extends StatelessWidget {
  const _TopBar({
    required this.title,
    required this.viewsLeft,
    required this.viewsTotal,
    required this.onBack,
  });

  final String title;
  final int? viewsLeft;
  final int? viewsTotal;
  final VoidCallback onBack;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.fromLTRB(10, 12, 14, 12),
      decoration: BoxDecoration(
        gradient: LinearGradient(
          begin: Alignment.topCenter,
          end: Alignment.bottomCenter,
          colors: <Color>[
            SbColors.playerDeep.withValues(alpha: 0.85),
            SbColors.playerDeep.withValues(alpha: 0.0),
          ],
        ),
      ),
      child: SafeArea(
        bottom: false,
        child: Row(
          children: <Widget>[
            _IconChip(icon: Icons.chevron_left, onTap: onBack),
            const SizedBox(width: 10),
            Expanded(
              child: Text(
                title,
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
                style: const TextStyle(
                  fontFamily: SbFonts.sans,
                  fontSize: 15,
                  fontWeight: FontWeight.w700,
                  color: SbColors.white,
                ),
              ),
            ),
            const SizedBox(width: 10),
            const EncryptedChip(),
            const SizedBox(width: 8),
            _ViewsLeftCounter(viewsLeft: viewsLeft, viewsTotal: viewsTotal),
          ],
        ),
      ),
    );
  }
}

/// "N of M views left" (`FR-APP-VID-004`), amber mono. Sourced from the redeem
/// manifest budget (`accessRemaining`/`accessAllowed`, contract §D): [viewsLeft]
/// is the post-Play remaining (the "N"), [viewsTotal] the total granted (the
/// "M"). Both `null` until the manifest arrives (or if an older API omits them)
/// → a clearly-marked fallback (`Views · secured`); the app never invents a count.
class _ViewsLeftCounter extends StatelessWidget {
  const _ViewsLeftCounter({required this.viewsLeft, required this.viewsTotal});

  final int? viewsLeft;
  final int? viewsTotal;

  @override
  Widget build(BuildContext context) {
    final int? n = viewsLeft;
    final int? m = viewsTotal;
    final bool known = n != null;
    final String text = !known
        ? 'Views · secured'
        : (m != null ? '$n of $m views left' : '$n views left');
    return Tooltip(
      message: known
          ? 'Views remaining for this lesson'
          : 'Remaining views are confirmed at the server.',
      child: Container(
        padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 6),
        decoration: BoxDecoration(
          color: SbColors.amber.withValues(alpha: 0.16),
          borderRadius: SbRadii.brPill,
          border: Border.all(color: SbColors.amber.withValues(alpha: 0.35)),
        ),
        child: Text(
          text,
          style: const TextStyle(
            fontFamily: SbFonts.mono,
            fontSize: 12,
            fontWeight: FontWeight.w700,
            color: SbColors.amber,
          ),
        ),
      ),
    );
  }
}

class _IconChip extends StatelessWidget {
  const _IconChip({required this.icon, required this.onTap});

  final IconData icon;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return Material(
      color: SbColors.white.withValues(alpha: 0.1),
      borderRadius: SbRadii.brInput,
      child: InkWell(
        borderRadius: SbRadii.brInput,
        onTap: onTap,
        child: SizedBox(
          width: 38,
          height: 38,
          child: Icon(icon, color: SbColors.white, size: 22),
        ),
      ),
    );
  }
}

class _StageSpinner extends StatelessWidget {
  const _StageSpinner();

  @override
  Widget build(BuildContext context) {
    return const SizedBox(
      width: 38,
      height: 38,
      child: CircularProgressIndicator(
        strokeWidth: 3,
        valueColor: AlwaysStoppedAnimation<Color>(SbColors.accentBlue),
      ),
    );
  }
}

/// Inline dark failure panel over the stage. Shows the §H verbatim title +
/// message + primary action; for the generic (no-§H) case the server `detail`
/// is already carried in [PlayerError.message].
class _PlayerErrorOverlay extends StatelessWidget {
  const _PlayerErrorOverlay({required this.error, required this.onPrimary});

  final PlayerError error;
  final VoidCallback onPrimary;

  @override
  Widget build(BuildContext context) {
    return ColoredBox(
      color: SbColors.playerBg.withValues(alpha: 0.96),
      child: Center(
        child: SingleChildScrollView(
          padding: const EdgeInsets.all(24),
          child: ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: 380),
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: <Widget>[
                Image.asset(SbAssets.failed, width: 108),
                const SizedBox(height: 14),
                Text(
                  error.title,
                  textAlign: TextAlign.center,
                  style: const TextStyle(
                    fontFamily: SbFonts.sans,
                    fontSize: 20,
                    fontWeight: FontWeight.w800,
                    color: SbColors.white,
                  ),
                ),
                const SizedBox(height: 12),
                Text(
                  error.message,
                  textAlign: TextAlign.center,
                  style: TextStyle(
                    fontFamily: SbFonts.sans,
                    fontSize: 14,
                    height: 1.55,
                    color: SbColors.white.withValues(alpha: 0.7),
                  ),
                ),
                const SizedBox(height: 20),
                SizedBox(
                  height: 46,
                  child: FilledButton(
                    onPressed: onPrimary,
                    style: FilledButton.styleFrom(
                      backgroundColor: SbColors.primary,
                      foregroundColor: SbColors.white,
                      shape: const RoundedRectangleBorder(
                        borderRadius: SbRadii.brInput,
                      ),
                      padding: const EdgeInsets.symmetric(horizontal: 28),
                    ),
                    child: Text(
                      error.primaryActionLabel,
                      style: const TextStyle(
                        fontFamily: SbFonts.sans,
                        fontSize: 14,
                        fontWeight: FontWeight.w700,
                      ),
                    ),
                  ),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}
