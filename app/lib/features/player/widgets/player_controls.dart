import 'package:flutter/material.dart';

import '../../../core/theme/sb_text.dart';
import '../../../core/theme/sb_tokens.dart';

/// Formats a [Duration] as `m:ss` (or `h:mm:ss` past an hour) for the mono
/// elapsed/remaining timers.
String formatPlaybackTime(Duration d) {
  final int total = d.inSeconds < 0 ? 0 : d.inSeconds;
  final int h = total ~/ 3600;
  final int m = (total % 3600) ~/ 60;
  final int s = total % 60;
  final String ss = s.toString().padLeft(2, '0');
  if (h > 0) {
    final String mm = m.toString().padLeft(2, '0');
    return '$h:$mm:$ss';
  }
  return '$m:$ss';
}

/// The player control bar (`PLAYER` banner, master §5.3): play/pause · seek bar
/// **with elapsed/remaining timers** · **speed 1× / 1.25× / 1.5× / 2×** ·
/// mute/volume · fullscreen. **No download/export control exists**
/// (`FR-APP-VID-005`). Pure presentational — wraps onto two rows when [compact].
class PlayerControls extends StatelessWidget {
  const PlayerControls({
    super.key,
    required this.position,
    required this.duration,
    required this.buffered,
    required this.playing,
    required this.muted,
    required this.fullscreen,
    required this.speed,
    required this.compact,
    required this.onPlayPause,
    required this.onSeek,
    required this.onCycleSpeed,
    required this.onToggleMute,
    required this.onToggleFullscreen,
  });

  final Duration position;
  final Duration duration;
  final Duration buffered;
  final bool playing;
  final bool muted;
  final bool fullscreen;
  final double speed;
  final bool compact;

  final VoidCallback onPlayPause;
  final ValueChanged<Duration> onSeek;
  final VoidCallback onCycleSpeed;
  final VoidCallback onToggleMute;
  final VoidCallback onToggleFullscreen;

  String get _speedLabel {
    final String n = speed == speed.roundToDouble()
        ? speed.toStringAsFixed(0)
        : speed.toString();
    return '$n×';
  }

  Duration get _remaining {
    final Duration r = duration - position;
    return r < Duration.zero ? Duration.zero : r;
  }

  @override
  Widget build(BuildContext context) {
    final Widget seek = _SeekBar(
      position: position,
      duration: duration,
      buffered: buffered,
      onSeek: onSeek,
    );

    final Widget timers = Row(
      mainAxisSize: MainAxisSize.min,
      children: <Widget>[
        _Timer(text: formatPlaybackTime(position)),
        const SizedBox(width: 8),
        Text(
          '/',
          style: TextStyle(
            fontFamily: SbFonts.mono,
            fontSize: 12,
            color: SbColors.white.withValues(alpha: 0.4),
          ),
        ),
        const SizedBox(width: 8),
        _Timer(text: '-${formatPlaybackTime(_remaining)}'),
      ],
    );

    final List<Widget> rightControls = <Widget>[
      _PillButton(label: _speedLabel, onTap: onCycleSpeed, tooltip: 'Speed'),
      const SizedBox(width: 8),
      _IconButton(
        icon: muted ? Icons.volume_off_rounded : Icons.volume_up_rounded,
        onTap: onToggleMute,
        tooltip: muted ? 'Unmute' : 'Mute',
      ),
      const SizedBox(width: 8),
      _IconButton(
        icon: fullscreen
            ? Icons.fullscreen_exit_rounded
            : Icons.fullscreen_rounded,
        onTap: onToggleFullscreen,
        tooltip: fullscreen ? 'Exit fullscreen' : 'Fullscreen',
      ),
    ];

    final Widget playPause = _IconButton(
      icon: playing ? Icons.pause_rounded : Icons.play_arrow_rounded,
      onTap: onPlayPause,
      primary: true,
      tooltip: playing ? 'Pause' : 'Play',
    );

    return Container(
      padding: const EdgeInsets.fromLTRB(14, 12, 14, 14),
      decoration: BoxDecoration(
        gradient: LinearGradient(
          begin: Alignment.bottomCenter,
          end: Alignment.topCenter,
          colors: <Color>[
            SbColors.playerDeep.withValues(alpha: 0.92),
            SbColors.playerDeep.withValues(alpha: 0.0),
          ],
        ),
      ),
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: <Widget>[
          seek,
          const SizedBox(height: 4),
          // Controls wrap on narrow widths: play+timers on one line, the
          // speed/mute/fullscreen cluster flows beneath when there's no room.
          Wrap(
            alignment: WrapAlignment.spaceBetween,
            crossAxisAlignment: WrapCrossAlignment.center,
            runSpacing: 10,
            spacing: 12,
            children: <Widget>[
              Row(
                mainAxisSize: MainAxisSize.min,
                children: <Widget>[
                  playPause,
                  const SizedBox(width: 12),
                  timers,
                ],
              ),
              Row(mainAxisSize: MainAxisSize.min, children: rightControls),
            ],
          ),
        ],
      ),
    );
  }
}

class _Timer extends StatelessWidget {
  const _Timer({required this.text});

  final String text;

  @override
  Widget build(BuildContext context) {
    return Text(
      text,
      style: const TextStyle(
        fontFamily: SbFonts.mono,
        fontSize: 12.5,
        color: SbColors.white,
      ),
    );
  }
}

class _SeekBar extends StatelessWidget {
  const _SeekBar({
    required this.position,
    required this.duration,
    required this.buffered,
    required this.onSeek,
  });

  final Duration position;
  final Duration duration;
  final Duration buffered;
  final ValueChanged<Duration> onSeek;

  @override
  Widget build(BuildContext context) {
    final double max = duration.inMilliseconds.toDouble();
    final double value = max <= 0
        ? 0
        : position.inMilliseconds.clamp(0, max.toInt()).toDouble();

    return SliderTheme(
      data: SliderThemeData(
        trackHeight: 4,
        activeTrackColor: SbColors.accentBlue,
        inactiveTrackColor: SbColors.white.withValues(alpha: 0.16),
        secondaryActiveTrackColor: SbColors.white.withValues(alpha: 0.28),
        thumbColor: SbColors.white,
        overlayColor: SbColors.accentBlue.withValues(alpha: 0.18),
        thumbShape: const RoundSliderThumbShape(enabledThumbRadius: 7),
        overlayShape: const RoundSliderOverlayShape(overlayRadius: 14),
      ),
      child: Slider(
        value: value,
        max: max <= 0 ? 1 : max,
        secondaryTrackValue: max <= 0
            ? 0
            : buffered.inMilliseconds.clamp(0, max.toInt()).toDouble(),
        onChanged: max <= 0
            ? null
            : (double v) => onSeek(Duration(milliseconds: v.round())),
      ),
    );
  }
}

class _IconButton extends StatelessWidget {
  const _IconButton({
    required this.icon,
    required this.onTap,
    required this.tooltip,
    this.primary = false,
  });

  final IconData icon;
  final VoidCallback onTap;
  final String tooltip;
  final bool primary;

  @override
  Widget build(BuildContext context) {
    final double size = primary ? 46 : 38;
    return Tooltip(
      message: tooltip,
      child: Material(
        color: primary
            ? SbColors.primary
            : SbColors.white.withValues(alpha: 0.1),
        shape: const CircleBorder(),
        child: InkWell(
          customBorder: const CircleBorder(),
          onTap: onTap,
          child: SizedBox(
            width: size,
            height: size,
            child: Icon(
              icon,
              color: SbColors.white,
              size: primary ? 28 : 21,
            ),
          ),
        ),
      ),
    );
  }
}

class _PillButton extends StatelessWidget {
  const _PillButton({
    required this.label,
    required this.onTap,
    required this.tooltip,
  });

  final String label;
  final VoidCallback onTap;
  final String tooltip;

  @override
  Widget build(BuildContext context) {
    return Tooltip(
      message: tooltip,
      child: Material(
        color: SbColors.white.withValues(alpha: 0.1),
        borderRadius: SbRadii.brPill,
        child: InkWell(
          borderRadius: SbRadii.brPill,
          onTap: onTap,
          child: Container(
            height: 38,
            alignment: Alignment.center,
            constraints: const BoxConstraints(minWidth: 52),
            padding: const EdgeInsets.symmetric(horizontal: 14),
            child: Text(
              label,
              style: const TextStyle(
                fontFamily: SbFonts.mono,
                fontSize: 13,
                fontWeight: FontWeight.w700,
                color: SbColors.white,
              ),
            ),
          ),
        ),
      ),
    );
  }
}
