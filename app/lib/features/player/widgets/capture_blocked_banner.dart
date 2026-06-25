import 'package:flutter/material.dart';

import '../../../core/secure_surface/secure_surface.dart';
import '../../../core/theme/sb_text.dart';
import '../../../core/theme/sb_tokens.dart';

/// The capture-state pill (`PLAYER` banner, master §5.3) — now **truthful**: it
/// reflects the **real** [SecureSurfaceStatus] the player engaged (A2), not a
/// static reassurance.
///
/// * [SecureSurfaceStatus.protected] — the OS black-out is active: the original
///   dark pill with the green `videocam_off` icon + **"Screen capture blocked"**
///   (`FR-APP-CAP-001`).
/// * [SecureSurfaceStatus.unsupported] — best-effort / unguaranteed (iOS
///   still-screenshot gap `NFR-APP-CAP-005`, or a platform where the black-out
///   can't be guaranteed): an **amber** warning. On desktop/Android this pairs
///   with the COMPAT-002 **warn + refuse** (the player won't play); on iOS it
///   plays best-effort behind the watermark.
/// * [SecureSurfaceStatus.off] — renders nothing (the mount site also gates it).
///
/// Tokens only (`SbColors`/`SbRadii`/`SbFonts`) — **no new hex**: the protected
/// pill keeps the existing green/black, the unsupported variant reuses
/// `SbColors.amber`.
class CaptureBlockedBanner extends StatelessWidget {
  const CaptureBlockedBanner({
    super.key,
    this.status = SecureSurfaceStatus.protected,
  });

  final SecureSurfaceStatus status;

  @override
  Widget build(BuildContext context) {
    if (status == SecureSurfaceStatus.off) return const SizedBox.shrink();

    final bool protected = status == SecureSurfaceStatus.protected;
    final Color accent = protected ? SbColors.greenSoft : SbColors.amber;
    final IconData icon = protected
        ? Icons.videocam_off_outlined
        : Icons.warning_amber_rounded;
    final String label = protected
        ? 'Screen capture blocked'
        : "Capture can't be blocked on this device";

    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 11, vertical: 6),
      decoration: BoxDecoration(
        color: SbColors.black.withValues(alpha: 0.42),
        borderRadius: SbRadii.brPill,
        border: Border.all(
          color: protected
              ? SbColors.white.withValues(alpha: 0.14)
              : SbColors.amber.withValues(alpha: 0.45),
        ),
      ),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: <Widget>[
          Icon(icon, size: 14, color: accent),
          const SizedBox(width: 6),
          Text(
            label,
            style: const TextStyle(
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
