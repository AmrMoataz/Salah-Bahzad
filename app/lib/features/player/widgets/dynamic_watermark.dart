import 'dart:async';

import 'package:flutter/material.dart';

import '../../../core/theme/sb_text.dart';
import '../../../core/theme/sb_tokens.dart';

/// The dual-layer anti-sharing watermark of `{serial} · {fullName}`
/// (`FR-APP-VID-003`, contract §C — serial + name, **never** phone):
///
///  * **Layer 1** — a faint, tiled diagonal wash across the whole frame (always
///    on, deters a clean re-record).
///  * **Layer 2** — a single brighter chip that **repositions every
///    `SbMotion.watermarkInterval` (2600 ms)** with a `watermarkReposition`
///    (1400 ms) eased move, so a cropped capture still catches it.
///
/// Pure & deterministic for goldens when [animate] is `false` (the chip sits at
/// the first slot and no timer runs — views never own a clock). Mono font,
/// legible-but-unobtrusive. [IgnorePointer] so it never eats player taps.
class DynamicWatermark extends StatefulWidget {
  const DynamicWatermark({super.key, required this.label, this.animate = true});

  /// `profile.watermarkLabel` = `"{serial} · {fullName}"`. Empty serial (until
  /// the backend stream lands) renders the name with a leading "· ".
  final String label;

  /// Goldens pass `false` for a still frame; the live player passes `true`.
  final bool animate;

  /// The repositioning slots for layer 2 — mirrors the prototype's `positions`.
  static const List<Alignment> _slots = <Alignment>[
    Alignment(-0.72, -0.52),
    Alignment(0.20, -0.32),
    Alignment(-0.40, 0.28),
    Alignment(0.32, 0.40),
    Alignment(-0.12, -0.68),
  ];

  @override
  State<DynamicWatermark> createState() => _DynamicWatermarkState();
}

class _DynamicWatermarkState extends State<DynamicWatermark> {
  int _slot = 0;
  Timer? _timer;

  @override
  void initState() {
    super.initState();
    if (widget.animate) {
      _timer = Timer.periodic(SbMotion.watermarkInterval, (_) {
        if (!mounted) return;
        setState(() {
          _slot = (_slot + 1) % DynamicWatermark._slots.length;
        });
      });
    }
  }

  @override
  void dispose() {
    _timer?.cancel();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return IgnorePointer(
      child: Stack(
        fit: StackFit.expand,
        children: <Widget>[
          _TiledWash(label: widget.label),
          AnimatedAlign(
            duration: SbMotion.watermarkReposition,
            curve: SbMotion.out,
            alignment: DynamicWatermark._slots[_slot],
            child: _WatermarkChip(label: widget.label),
          ),
        ],
      ),
    );
  }
}

/// Layer 1 — a faint, repeating diagonal wash of the label.
class _TiledWash extends StatelessWidget {
  const _TiledWash({required this.label});

  final String label;

  @override
  Widget build(BuildContext context) {
    return Opacity(
      opacity: 0.05,
      child: ClipRect(
        child: Transform.rotate(
          angle: -0.42,
          child: OverflowBox(
            maxWidth: double.infinity,
            maxHeight: double.infinity,
            child: Column(
              mainAxisAlignment: MainAxisAlignment.center,
              children: <Widget>[
                for (int row = 0; row < 14; row++)
                  Padding(
                    padding: const EdgeInsets.symmetric(vertical: 18),
                    child: Row(
                      mainAxisAlignment: MainAxisAlignment.center,
                      children: <Widget>[
                        for (int col = 0; col < 6; col++)
                          Padding(
                            padding: const EdgeInsets.symmetric(horizontal: 26),
                            child: Text(
                              label,
                              maxLines: 1,
                              softWrap: false,
                              overflow: TextOverflow.visible,
                              style: const TextStyle(
                                fontFamily: SbFonts.mono,
                                fontSize: 13,
                                color: SbColors.white,
                              ),
                            ),
                          ),
                      ],
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

/// Layer 2 — the single brighter repositioning chip.
class _WatermarkChip extends StatelessWidget {
  const _WatermarkChip({required this.label});

  final String label;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 7),
      decoration: BoxDecoration(
        color: SbColors.black.withValues(alpha: 0.28),
        borderRadius: SbRadii.brPill,
      ),
      child: Text(
        label,
        maxLines: 1,
        softWrap: false,
        overflow: TextOverflow.ellipsis,
        style: TextStyle(
          fontFamily: SbFonts.mono,
          fontSize: 12,
          letterSpacing: 0.4,
          color: SbColors.white.withValues(alpha: 0.34),
        ),
      ),
    );
  }
}
