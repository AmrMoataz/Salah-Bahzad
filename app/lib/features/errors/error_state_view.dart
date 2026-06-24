import 'package:flutter/material.dart';

import '../../core/theme/sb_assets.dart';
import '../../core/theme/sb_text.dart';
import '../../core/theme/sb_tokens.dart';

/// Design anchor: `FAILURE / RETRY`. The shared recoverable-failure layout —
/// failed mascot, title + message, a primary (and optional secondary) action,
/// and the reassurance footer. A0 uses it for a malformed deep link; A3 wires
/// the full seven-state set (contract §H).
class ErrorStateView extends StatelessWidget {
  const ErrorStateView({
    super.key,
    required this.title,
    required this.message,
    required this.primaryLabel,
    required this.onPrimary,
    this.secondaryLabel,
    this.onSecondary,
  });

  final String title;
  final String message;
  final String primaryLabel;
  final VoidCallback onPrimary;
  final String? secondaryLabel;
  final VoidCallback? onSecondary;

  @override
  Widget build(BuildContext context) {
    return ColoredBox(
      color: SbColors.paper,
      child: Padding(
        padding: const EdgeInsets.all(22),
        child: Column(
          children: <Widget>[
            const Spacer(),
            ConstrainedBox(
              constraints: const BoxConstraints(maxWidth: 400),
              child: Column(
                mainAxisSize: MainAxisSize.min,
                children: <Widget>[
                  Image.asset(SbAssets.failed, width: 120),
                  const SizedBox(height: 14),
                  Text(
                    title,
                    textAlign: TextAlign.center,
                    style: const TextStyle(
                      fontFamily: SbFonts.sans,
                      fontSize: 21,
                      fontWeight: FontWeight.w800,
                      color: SbColors.ink,
                    ),
                  ),
                  const SizedBox(height: 14),
                  ConstrainedBox(
                    constraints: const BoxConstraints(maxWidth: 330),
                    child: Text(
                      message,
                      textAlign: TextAlign.center,
                      style: const TextStyle(
                        fontFamily: SbFonts.sans,
                        fontSize: 14,
                        height: 1.55,
                        color: SbColors.ink4,
                      ),
                    ),
                  ),
                  const SizedBox(height: 18),
                  ConstrainedBox(
                    constraints: const BoxConstraints(maxWidth: 280),
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.stretch,
                      children: <Widget>[
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
                            ),
                            child: Text(
                              primaryLabel,
                              style: const TextStyle(
                                fontFamily: SbFonts.sans,
                                fontSize: 14,
                                fontWeight: FontWeight.w700,
                              ),
                            ),
                          ),
                        ),
                        if (secondaryLabel != null) ...<Widget>[
                          const SizedBox(height: 9),
                          SizedBox(
                            height: 44,
                            child: OutlinedButton(
                              onPressed: onSecondary,
                              style: OutlinedButton.styleFrom(
                                foregroundColor: SbColors.ink3,
                                side: const BorderSide(
                                  color: SbColors.border,
                                  width: 1.5,
                                ),
                                shape: const RoundedRectangleBorder(
                                  borderRadius: SbRadii.brInput,
                                ),
                              ),
                              child: Text(
                                secondaryLabel!,
                                style: const TextStyle(
                                  fontFamily: SbFonts.sans,
                                  fontSize: 14,
                                  fontWeight: FontWeight.w700,
                                ),
                              ),
                            ),
                          ),
                        ],
                      ],
                    ),
                  ),
                ],
              ),
            ),
            const Spacer(),
            const Text(
              'Your place is saved — nothing is lost.',
              style: TextStyle(
                fontFamily: SbFonts.sans,
                fontSize: 12,
                color: SbColors.ink6,
              ),
            ),
          ],
        ),
      ),
    );
  }
}
