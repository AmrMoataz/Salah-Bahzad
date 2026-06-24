import 'package:flutter/widgets.dart';

import 'sb_tokens.dart';

/// Font families bundled in `pubspec.yaml` (mirrored from the design system).
class SbFonts {
  SbFonts._();

  /// Body / UI text.
  static const String sans = 'Nunito Sans';

  /// Brand display headings ("Secure Player", "Welcome back").
  static const String marker = 'Permanent Marker';

  /// The faint math-doodle motif behind dark panels.
  static const String doodle = 'Caveat';

  /// Codes / timers / the watermark.
  static const String mono = 'Cascadia Mono';
}

/// Reusable text styles. Kept thin — most screens compose [TextStyle] inline
/// against [SbFonts] + [SbColors], matching the prototype's per-element sizing.
class SbText {
  SbText._();

  static const TextStyle brand = TextStyle(
    fontFamily: SbFonts.marker,
    fontSize: 28,
    height: 1.1,
    letterSpacing: 0.5,
    color: SbColors.white,
  );

  static const TextStyle h1 = TextStyle(
    fontFamily: SbFonts.sans,
    fontSize: 22,
    fontWeight: FontWeight.w800,
    color: SbColors.ink,
  );

  static const TextStyle body = TextStyle(
    fontFamily: SbFonts.sans,
    fontSize: 15,
    height: 1.5,
    color: SbColors.ink3,
  );

  static const TextStyle label = TextStyle(
    fontFamily: SbFonts.sans,
    fontSize: 12,
    fontWeight: FontWeight.w700,
    color: SbColors.ink2,
  );

  static const TextStyle monoSmall = TextStyle(
    fontFamily: SbFonts.mono,
    fontSize: 12,
    color: SbColors.ink5,
  );
}
