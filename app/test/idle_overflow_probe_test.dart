import 'package:flutter/foundation.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:secure_player/core/theme/sb_theme.dart';
import 'package:secure_player/features/idle/idle_view.dart';

void _noop() {}

void main() {
  testWidgets('long name on compact width overflows', (tester) async {
    final List<FlutterErrorDetails> errors = <FlutterErrorDetails>[];
    final FlutterExceptionHandler? prev = FlutterError.onError;
    FlutterError.onError = (FlutterErrorDetails d) => errors.add(d);

    tester.view.devicePixelRatio = 1.0;
    tester.view.physicalSize = const Size(360, 1200);
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    await tester.pumpWidget(MaterialApp(
      debugShowCheckedModeBanner: false,
      theme: SbTheme.build(),
      home: const Material(
        type: MaterialType.transparency,
        child: IdleView(
          fullName: 'Abdul Rahman Mohammed Al-Hassan',
          signedInAs: 'Abdul Rahman Mohammed Al-Hassan',
          onOpenPortal: _noop,
          onSignOut: _noop,
        ),
      ),
    ));
    await tester.pump();

    FlutterError.onError = prev;

    final overflowErrors = errors
        .where((e) => e.exceptionAsString().contains('overflowed'))
        .toList();
    // ignore: avoid_print
    print('OVERFLOW_COUNT=${overflowErrors.length}');
    for (final e in overflowErrors) {
      // ignore: avoid_print
      print('OVERFLOW_MSG=${e.exceptionAsString()}');
    }
  });
}
