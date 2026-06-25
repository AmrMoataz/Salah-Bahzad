import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:secure_player/core/theme/sb_theme.dart';
import 'package:secure_player/core/theme/sb_tokens.dart';
import 'package:secure_player/features/player/player_state.dart';
import 'package:secure_player/features/player/player_view.dart';

import '../support/test_fonts.dart';

/// Goldens prove the Player reflows across the master-plan breakpoints (compact
/// 360 / medium 768 / expanded 1280) and that the controls wrap on compact.
/// They render the pure [PlayerView] with a fixed state, a static (non-animated)
/// watermark and a placeholder video surface — no engine, timer, or network.
void main() {
  const List<double> widths = <double>[360, 768, 1280];

  setUpAll(loadAppFonts);

  Widget wrap(Widget child) => MaterialApp(
    debugShowCheckedModeBanner: false,
    theme: SbTheme.build(),
    home: Material(type: MaterialType.transparency, child: child),
  );

  // A visible stand-in for the libmpv render surface.
  Widget surface() => const ColoredBox(color: SbColors.playerStageA);

  PlayerView view(PlayerState state) => PlayerView(
    state: state,
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
    // The loading state shows an indefinite spinner, so `pumpAndSettle` would
    // never settle — pump a fixed frame for a deterministic golden instead.
    await tester.pump(const Duration(milliseconds: 50));
  }

  const PlayerState playing = PlayerState(
    status: PlayerStatus.playing,
    position: Duration(seconds: 42),
    duration: Duration(seconds: 320),
    buffered: Duration(seconds: 96),
    speed: 1.25,
    viewsLeft: 2,
    viewsTotal: 3,
  );

  const PlayerState loading = PlayerState();

  for (final double w in widths) {
    final String tag = w.toInt().toString();

    testWidgets('player playing @ $tag', (WidgetTester tester) async {
      await pumpAt(tester, view(playing), w);
      await expectLater(
        find.byType(PlayerView),
        matchesGoldenFile('goldens/player_playing_$tag.png'),
      );
    });

    testWidgets('player loading @ $tag', (WidgetTester tester) async {
      await pumpAt(tester, view(loading), w);
      await expectLater(
        find.byType(PlayerView),
        matchesGoldenFile('goldens/player_loading_$tag.png'),
      );
    });
  }
}
