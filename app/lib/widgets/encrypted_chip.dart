import 'package:flutter/material.dart';

import '../core/theme/sb_text.dart';
import '../core/theme/sb_tokens.dart';

/// The green **Encrypted** chip (shield + label) in the player top bar — the
/// at-a-glance "this stream is AES-128 encrypted" reassurance (`PLAYER` banner,
/// master §5.3). Promoted from the A0 placeholder's inlined `_EncryptedChip`.
class EncryptedChip extends StatelessWidget {
  const EncryptedChip({super.key});

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 11, vertical: 6),
      decoration: BoxDecoration(
        color: SbColors.white.withValues(alpha: 0.1),
        borderRadius: SbRadii.brPill,
      ),
      child: const Row(
        mainAxisSize: MainAxisSize.min,
        children: <Widget>[
          Icon(Icons.shield_outlined, size: 14, color: SbColors.greenSoft),
          SizedBox(width: 6),
          Text(
            'Encrypted',
            style: TextStyle(
              fontFamily: SbFonts.sans,
              fontSize: 12,
              fontWeight: FontWeight.w700,
              color: SbColors.white,
            ),
          ),
        ],
      ),
    );
  }
}
