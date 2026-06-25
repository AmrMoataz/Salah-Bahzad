import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:secure_player/core/secure_surface/secure_surface.dart';
import 'package:secure_player/core/theme/sb_theme.dart';
import 'package:secure_player/core/theme/sb_tokens.dart';
import 'package:secure_player/features/player/player_state.dart';
import 'package:secure_player/features/player/player_view.dart';

import '../support/test_fonts.dart';

/// A2 goldens: the capture banner reflects the **real** protection state — the
/// green `protected` pill and the amber `unsupported` warning (F6) — rendered in
/// the player across the master-plan breakpoints (360 / 768 / 1280). Pure
/// [PlayerView] with a fixed state + static watermark + placeholder surface; no
/// engine/channel/network.
void main() {
  const List<double> widths = <double>[360, 768, 1280];

  setUpAll(loadAppFonts);

  Widget wrap(Widget child) => MaterialApp(
    debugShowCheckedModeBanner: false,
    theme: SbTheme.build(),
    home: Material(type: MaterialType.transparency, child: child),
  );

  Widget surface() => const ColoredBox(color: SbColors.playerStageA);

  PlayerView view(SecureSurfaceStatus status) => PlayerView(
    state: const PlayerState(
      status: PlayerStatus.playing,
      position: Duration(seconds: 42),
      duration: Duration(seconds: 320),
      buffered: Duration(seconds: 96),
      viewsLeft: 2,
      viewsTotal: 3,
    ),
    captureStatus: status,
    lessonTitle: 'Algebra · Quadratic equations',
    watermarkLabel: 'STU-7K2M9X · Layla Ahmed',
    videoSurface: surface(),
    animateWatermark: false,
    onBack: () {},
    onPlayPause: () {},
    onSeek: (_) {},
    onCycleSpeed: () {},
    onToggleMute: () {},
    onToggleFullscreen: () {},
    onPrimaryAction: () {},
  );

  Future<void> pumpAt(WidgetTester tester, Widget child, double width) async {
    tester.view.devicePixelRatio = 1.0;
    tester.view.physicalSize = Size(width, 720);
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);
    await tester.pumpWidget(wrap(child));
    await tester.pump(const Duration(milliseconds: 50));
  }

  for (final double w in widths) {
    final String tag = w.toInt().toString();

    testWidgets('capture banner protected (green) @ $tag', (
      WidgetTester tester,
    ) async {
      await pumpAt(tester, view(SecureSurfaceStatus.protected), w);
      await expectLater(
        find.byType(PlayerView),
        matchesGoldenFile('goldens/capture_protected_$tag.png'),
      );
    });

    testWidgets('capture banner unsupported (amber) @ $tag', (
      WidgetTester tester,
    ) async {
      await pumpAt(tester, view(SecureSurfaceStatus.unsupported), w);
      await expectLater(
        find.byType(PlayerView),
        matchesGoldenFile('goldens/capture_unsupported_$tag.png'),
      );
    });
  }
}
