import 'package:flutter/material.dart';

import 'sb_text.dart';
import 'sb_tokens.dart';

/// The app-wide [ThemeData]. The Secure Player is a light, paper-themed product
/// (the dark surfaces — splash, player — paint their own backgrounds locally).
class SbTheme {
  SbTheme._();

  static ThemeData build() {
    final ColorScheme scheme = ColorScheme.fromSeed(
      seedColor: SbColors.primary,
      primary: SbColors.primary,
      surface: SbColors.paper,
    );

    return ThemeData(
      useMaterial3: true,
      colorScheme: scheme,
      scaffoldBackgroundColor: SbColors.paper,
      fontFamily: SbFonts.sans,
      visualDensity: VisualDensity.standard,
      splashFactory: InkRipple.splashFactory,
      textSelectionTheme: const TextSelectionThemeData(
        cursorColor: SbColors.primary,
        selectionHandleColor: SbColors.primary,
      ),
      textTheme: const TextTheme(
        bodyMedium: SbText.body,
        titleLarge: SbText.h1,
        labelMedium: SbText.label,
      ),
    );
  }
}
