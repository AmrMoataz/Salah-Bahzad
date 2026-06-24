import 'package:flutter/widgets.dart';

/// Design tokens for the Secure Player, extracted **verbatim** from the design
/// source of truth: `.claude/Salah Bahzad App/Secure Video App (standalone).html`
/// (master plan §5.2). These are the only place raw hex/values live — every
/// widget references [SbColors] / [SbSpace] / [SbRadii] / [SbMotion], never an
/// inline literal. If a value must change, change it here.
class SbColors {
  SbColors._();

  // — Brand / blue —
  static const Color primary = Color(0xFF2C6FB3);
  static const Color primaryHover = Color(0xFF245C95);
  static const Color navy = Color(0xFF1E3A5F);
  static const Color navyDeep = Color(0xFF16263D);
  static const Color navyDeepest = Color(0xFF122739);
  static const Color heroNavy = Color(0xFF27537E);
  static const Color splashNavy = Color(0xFF2A517A);
  static const Color accentBlue = Color(0xFF3E8EDE); // progress / focus accent
  static const Color accentBlueSoft = Color(0xFF9FC4EC); // accents on dark

  // — Paper —
  static const Color paper = Color(0xFFFBFBF7);
  static const Color paperAlt = Color(0xFFF4F3ED);
  static const Color board = Color(0xFFE7E5DF);
  static const Color paperHover = Color(0xFFF6F6F0);

  // — Player dark —
  static const Color playerBg = Color(0xFF0E1620);
  static const Color playerMid = Color(0xFF0A111C);
  static const Color playerDeep = Color(0xFF05090F);
  static const Color playerStageA = Color(0xFF15233A);
  static const Color deviceFrame = Color(0xFF0C1118);

  // — Secure green —
  static const Color green = Color(0xFF46A33E);
  static const Color greenSoft = Color(0xFF86C7A6);
  static const Color greenBright = Color(0xFF7FC375);
  static const Color greenText = Color(0xFF2A6326);
  static const Color greenBg = Color(0xFFEBF5E9);
  static const Color greenBorder = Color(0xFFABD7A3);

  // — Amber —
  static const Color amber = Color(0xFFF3C12E);
  static const Color amberText = Color(0xFF8A6A00);
  static const Color amberBg = Color(0xFFFEF6DD);
  static const Color amberBorder = Color(0xFFF5E2A0);

  // — Text ramp —
  static const Color ink = Color(0xFF1A1A16);
  static const Color ink2 = Color(0xFF3D3C35);
  static const Color ink3 = Color(0xFF54534A);
  static const Color ink4 = Color(0xFF6F6E63);
  static const Color ink5 = Color(0xFF98968A);
  static const Color ink6 = Color(0xFFA8A697);

  // — Borders —
  static const Color border = Color(0xFFDAD8CC);
  static const Color borderSoft = Color(0xFFECEBE2);
  static const Color borderDeviceEdge = Color(0xFFCFCDC2);

  // — Status chips (info / blue) —
  static const Color infoText = Color(0xFF1E4A78);
  static const Color infoBg = Color(0xFFE8F2FC);
  static const Color infoBorder = Color(0xFFA8C9EC);

  // — Danger / sign-out hover / window close —
  static const Color danger = Color(0xFFA52A24);
  static const Color dangerBorder = Color(0xFFE0B4B0);
  static const Color windowClose = Color(0xFFE5484D);

  // — Plain —
  static const Color white = Color(0xFFFFFFFF);
  static const Color black = Color(0xFF000000);

  // — macOS traffic-lights —
  static const Color macClose = Color(0xFFFF5F57);
  static const Color macMin = Color(0xFFFEBC2E);
  static const Color macZoom = Color(0xFF28C840);
}

/// Spacing scale (px) — the prototype uses an ad-hoc but consistent rhythm.
class SbSpace {
  SbSpace._();
  static const double x2 = 2;
  static const double x4 = 4;
  static const double x6 = 6;
  static const double x8 = 8;
  static const double x10 = 10;
  static const double x12 = 12;
  static const double x14 = 14;
  static const double x16 = 16;
  static const double x18 = 18;
  static const double x20 = 20;
  static const double x24 = 24;
  static const double x28 = 28;
  static const double x32 = 32;
  static const double x44 = 44;
}

/// Corner radii — cards 12–20, pills 999, device frame 42–56 (master §5.2).
class SbRadii {
  SbRadii._();
  static const Radius pill = Radius.circular(999);
  static const Radius card = Radius.circular(14);
  static const Radius cardLg = Radius.circular(20);
  static const Radius input = Radius.circular(10);
  static const Radius chip = Radius.circular(999);

  static const BorderRadius brCard = BorderRadius.all(card);
  static const BorderRadius brCardLg = BorderRadius.all(cardLg);
  static const BorderRadius brInput = BorderRadius.all(input);
  static const BorderRadius brPill = BorderRadius.all(pill);
}

/// Motion — mirrors the prototype's `.15s ease` / `.4s ease` keyframes.
class SbMotion {
  SbMotion._();
  static const Duration fast = Duration(milliseconds: 150);
  static const Duration enter = Duration(milliseconds: 400);
  static const Duration watermarkReposition = Duration(milliseconds: 1400);
  static const Duration watermarkInterval = Duration(milliseconds: 2600);
  static const Curve standard = Curves.easeInOut;
  static const Curve out = Curves.easeOut;
}
