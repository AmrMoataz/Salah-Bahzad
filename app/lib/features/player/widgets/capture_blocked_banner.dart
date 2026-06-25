import 'package:flutter/material.dart';

import '../../../core/theme/sb_text.dart';
import '../../../core/theme/sb_tokens.dart';

/// The static **"Screen capture blocked"** pill (`PLAYER` banner, master §5.3).
///
/// **A1 ships only this reassurance banner** — the real OS capture black-out
/// (`core/secure_surface`) is **A2**. It states the protection that A2 will
/// enforce; it does not itself block anything.
class CaptureBlockedBanner extends StatelessWidget {
  const CaptureBlockedBanner({super.key});

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 11, vertical: 6),
      decoration: BoxDecoration(
        color: SbColors.black.withValues(alpha: 0.42),
        borderRadius: SbRadii.brPill,
        border: Border.all(color: SbColors.white.withValues(alpha: 0.14)),
      ),
      child: const Row(
        mainAxisSize: MainAxisSize.min,
        children: <Widget>[
          Icon(Icons.videocam_off_outlined, size: 14, color: SbColors.greenSoft),
          SizedBox(width: 6),
          Text(
            'Screen capture blocked',
            style: TextStyle(
              fontFamily: SbFonts.sans,
              fontSize: 11.5,
              fontWeight: FontWeight.w700,
              color: SbColors.white,
            ),
          ),
        ],
      ),
    );
  }
}
