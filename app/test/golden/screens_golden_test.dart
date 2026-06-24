import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:secure_player/core/theme/sb_theme.dart';
import 'package:secure_player/features/idle/idle_view.dart';
import 'package:secure_player/features/signin/sign_in_view.dart';
import 'package:secure_player/features/splash/splash_view.dart';

import '../support/test_fonts.dart';

/// Golden tests prove the screens reflow correctly across the master-plan
/// breakpoints (compact 360 / medium 768 / expanded 1280). They render the pure
/// presentational views with fixed inputs — no timers, network, or Firebase.
void main() {
  const List<double> widths = <double>[360, 768, 1280];

  setUpAll(loadAppFonts);

  Widget wrap(Widget child) => MaterialApp(
    debugShowCheckedModeBanner: false,
    theme: SbTheme.build(),
    home: Material(type: MaterialType.transparency, child: child),
  );

  Future<void> pumpAt(WidgetTester tester, Widget child, double width) async {
    tester.view.devicePixelRatio = 1.0;
    tester.view.physicalSize = Size(width, 1200);
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    await tester.pumpWidget(wrap(child));
    await tester.pumpAndSettle();

    // Decode bundled images so the goldens show the mascots/logos.
    await tester.runAsync(() async {
      for (final Element element in find.byType(Image).evaluate()) {
        final Image image = element.widget as Image;
        await precacheImage(image.image, element);
      }
    });
    await tester.pumpAndSettle();
  }

  for (final double w in widths) {
    final String tag = w.toInt().toString();

    testWidgets('splash @ $tag', (WidgetTester tester) async {
      await pumpAt(tester, const SplashView(step: 1), w);
      await expectLater(
        find.byType(SplashView),
        matchesGoldenFile('goldens/splash_$tag.png'),
      );
    });

    testWidgets('sign in @ $tag', (WidgetTester tester) async {
      final TextEditingController email = TextEditingController(
        text: 'layla.ahmed@student.sb',
      );
      final TextEditingController password = TextEditingController(
        text: 'supersecret',
      );
      addTearDown(email.dispose);
      addTearDown(password.dispose);

      await pumpAt(
        tester,
        SignInView(
          emailController: email,
          passwordController: password,
          rememberMe: true,
          onRememberChanged: (_) {},
          onSignIn: () {},
          onGoogle: () {},
        ),
        w,
      );
      await expectLater(
        find.byType(SignInView),
        matchesGoldenFile('goldens/signin_$tag.png'),
      );
    });

    testWidgets('idle @ $tag', (WidgetTester tester) async {
      await pumpAt(
        tester,
        const IdleView(
          fullName: 'Layla Ahmed',
          signedInAs: 'Layla Ahmed',
          onOpenPortal: _noop,
          onSignOut: _noop,
        ),
        w,
      );
      await expectLater(
        find.byType(IdleView),
        matchesGoldenFile('goldens/idle_$tag.png'),
      );
    });
  }
}

void _noop() {}
