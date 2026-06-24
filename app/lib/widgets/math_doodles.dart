import 'package:flutter/widgets.dart';

import '../core/theme/sb_text.dart';

/// The faint hand-written math motif (Caveat) behind the navy panels — the
/// splash, the sign-in brand panel, and the Idle hero. Decorative only.
class MathDoodles extends StatelessWidget {
  const MathDoodles({
    super.key,
    this.color = const Color(0xFFFFFFFF),
    this.opacity = 0.08,
    this.fontSize = 32,
  });

  final Color color;
  final double opacity;
  final double fontSize;

  // ASCII math so the handwritten Caveat face renders every glyph (special
  // unicode like √ ∑ ∫ π Δ has no Caveat glyph and would tofu).
  static const String _glyphs =
      'x^2 + bx + c = 0    sqrt(a^2 + b^2)    pi r^2    f(x) dx    '
      'sin t = cos p    dy/dx    log b    x = (-b +/- sqrt(D)) / 2a    '
      'e^(i pi) + 1 = 0    a/b    (a + b)^2';

  @override
  Widget build(BuildContext context) {
    return Positioned.fill(
      child: IgnorePointer(
        child: ClipRect(
          child: Opacity(
            opacity: opacity,
            child: Padding(
              padding: const EdgeInsets.all(14),
              child: Text(
                _glyphs,
                maxLines: 12,
                overflow: TextOverflow.clip,
                style: TextStyle(
                  fontFamily: SbFonts.doodle,
                  fontSize: fontSize,
                  height: 2.1,
                  letterSpacing: 1,
                  color: color,
                ),
              ),
            ),
          ),
        ),
      ),
    );
  }
}
