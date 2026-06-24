import 'package:flutter/material.dart';

import '../../core/theme/sb_assets.dart';
import '../../core/theme/sb_text.dart';
import '../../core/theme/sb_tokens.dart';
import '../../widgets/math_doodles.dart';

/// Design anchor: `SPLASH / DEEP-LINK HANDLER`. Pure & deterministic (no timers)
/// so it can be golden-tested; [SplashPage] drives [step] (0→3) over time.
class SplashView extends StatelessWidget {
  const SplashView({super.key, this.step = 1, this.onTap});

  /// 0 = nothing done, 3 = all three steps complete.
  final int step;
  final VoidCallback? onTap;

  static const List<String> _labels = <String>[
    'Secure link received',
    'Verifying handoff code',
    'Opening secure session',
  ];

  @override
  Widget build(BuildContext context) {
    final double pct = (step / 3 * 100).clamp(0, 100).toDouble();

    return GestureDetector(
      onTap: onTap,
      child: Container(
        decoration: const BoxDecoration(
          gradient: RadialGradient(
            center: Alignment(0, -1.1),
            radius: 1.25,
            colors: <Color>[
              SbColors.splashNavy,
              SbColors.navy,
              SbColors.navyDeepest,
            ],
            stops: <double>[0, 0.48, 1],
          ),
        ),
        child: Stack(
          children: <Widget>[
            const MathDoodles(opacity: 0.07, fontSize: 34),
            SafeArea(
              child: Center(
                child: SingleChildScrollView(
                  padding: const EdgeInsets.all(32),
                  child: Column(
                    mainAxisAlignment: MainAxisAlignment.center,
                    children: <Widget>[
                      Image.asset(SbAssets.logoWhite, height: 30),
                      const SizedBox(height: 22),
                      Image.asset(SbAssets.mascot, width: 112),
                      const SizedBox(height: 22),
                      const Text(
                        'Secure Player',
                        style: TextStyle(
                          fontFamily: SbFonts.marker,
                          fontSize: 26,
                          letterSpacing: 0.5,
                          color: SbColors.white,
                        ),
                      ),
                      const SizedBox(height: 2),
                      Text(
                        'Salah Bahzad · protected lessons',
                        style: TextStyle(
                          fontFamily: SbFonts.sans,
                          fontSize: 13,
                          color: SbColors.white.withValues(alpha: 0.7),
                        ),
                      ),
                      const SizedBox(height: 22),
                      _StepsCard(step: step, pct: pct),
                      const SizedBox(height: 22),
                      Text.rich(
                        TextSpan(
                          children: <InlineSpan>[
                            const TextSpan(
                              text: 'salah-bahazad://stream?videoId=ALG-204\n',
                            ),
                            const TextSpan(
                              text: 'handoff ••••••••  ·  no token in URL',
                            ),
                          ],
                        ),
                        textAlign: TextAlign.center,
                        style: TextStyle(
                          fontFamily: SbFonts.mono,
                          fontSize: 11,
                          height: 1.7,
                          color: SbColors.white.withValues(alpha: 0.55),
                        ),
                      ),
                    ],
                  ),
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _StepsCard extends StatelessWidget {
  const _StepsCard({required this.step, required this.pct});

  final int step;
  final double pct;

  @override
  Widget build(BuildContext context) {
    return Container(
      constraints: const BoxConstraints(maxWidth: 360),
      padding: const EdgeInsets.symmetric(horizontal: 18, vertical: 16),
      decoration: BoxDecoration(
        color: SbColors.white.withValues(alpha: 0.08),
        borderRadius: SbRadii.brCard,
        border: Border.all(color: SbColors.white.withValues(alpha: 0.18)),
      ),
      child: Column(
        children: <Widget>[
          for (int i = 0; i < SplashView._labels.length; i++) ...<Widget>[
            _StepRow(
              label: SplashView._labels[i],
              done: step >= i + 1,
              active: step == i,
            ),
            if (i < SplashView._labels.length - 1) const SizedBox(height: 12),
          ],
          const SizedBox(height: 12),
          ClipRRect(
            borderRadius: SbRadii.brPill,
            child: Stack(
              children: <Widget>[
                Container(
                  height: 5,
                  color: SbColors.white.withValues(alpha: 0.14),
                ),
                FractionallySizedBox(
                  widthFactor: (pct / 100).clamp(0, 1).toDouble(),
                  child: Container(
                    height: 5,
                    decoration: const BoxDecoration(
                      gradient: LinearGradient(
                        colors: <Color>[
                          SbColors.accentBlue,
                          SbColors.greenSoft,
                        ],
                      ),
                    ),
                  ),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

class _StepRow extends StatelessWidget {
  const _StepRow({
    required this.label,
    required this.done,
    required this.active,
  });

  final String label;
  final bool done;
  final bool active;

  @override
  Widget build(BuildContext context) {
    final Color dot = done
        ? SbColors.greenBright
        : (active ? SbColors.amber : SbColors.white.withValues(alpha: 0.28));

    return Row(
      children: <Widget>[
        Container(
          width: 20,
          height: 20,
          decoration: BoxDecoration(color: dot, shape: BoxShape.circle),
          alignment: Alignment.center,
          child: done
              ? const Icon(Icons.check, size: 12, color: SbColors.navyDeep)
              : (active
                    ? Container(
                        margin: const EdgeInsets.all(3),
                        decoration: BoxDecoration(
                          shape: BoxShape.circle,
                          border: Border.all(
                            color: SbColors.white.withValues(alpha: 0.9),
                            width: 2,
                          ),
                        ),
                      )
                    : null),
        ),
        const SizedBox(width: 12),
        Expanded(
          child: Text(
            label,
            style: TextStyle(
              fontFamily: SbFonts.sans,
              fontSize: 14,
              color: SbColors.white.withValues(alpha: 0.92),
            ),
          ),
        ),
      ],
    );
  }
}
