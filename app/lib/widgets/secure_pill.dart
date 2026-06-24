import 'package:flutter/widgets.dart';

import '../core/theme/sb_text.dart';
import '../core/theme/sb_tokens.dart';

/// The green "Secure" status pill (a dot + label). Two tones: [SecurePill.light]
/// for paper surfaces (the Idle header) and [SecurePill.dark] for navy/player
/// surfaces (the window chrome).
class SecurePill extends StatelessWidget {
  const SecurePill.light({super.key}) : onDark = false;
  const SecurePill.dark({super.key}) : onDark = true;

  final bool onDark;

  @override
  Widget build(BuildContext context) {
    final Color bg = onDark
        ? const Color(0x2E46A33E) // rgba(70,163,62,.18)
        : SbColors.greenBg;
    final Color border = onDark
        ? const Color(0x5986C7A6) // rgba(134,199,166,.35)
        : SbColors.greenBorder;
    final Color fg = onDark ? const Color(0xFF9FE0A6) : SbColors.greenText;
    final Color dot = onDark ? SbColors.greenBright : SbColors.green;

    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 11, vertical: 5),
      decoration: BoxDecoration(
        color: bg,
        borderRadius: SbRadii.brPill,
        border: Border.all(color: border),
      ),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: <Widget>[
          Container(
            width: 8,
            height: 8,
            decoration: BoxDecoration(color: dot, shape: BoxShape.circle),
          ),
          const SizedBox(width: 7),
          Text(
            'Secure',
            style: TextStyle(
              fontFamily: SbFonts.sans,
              fontSize: 12,
              fontWeight: FontWeight.w700,
              color: fg,
            ),
          ),
        ],
      ),
    );
  }
}
