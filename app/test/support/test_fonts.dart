import 'dart:io';

import 'package:flutter/services.dart';

/// Loads the bundled brand fonts from disk so golden tests render real glyphs
/// (otherwise text falls back to the test placeholder font). Run once in
/// `setUpAll`. Paths are relative to the package root — `flutter test`'s cwd.
Future<void> loadAppFonts() async {
  Future<void> load(String family, List<String> paths) async {
    final FontLoader loader = FontLoader(family);
    for (final String path in paths) {
      loader.addFont(
        File(path).readAsBytes().then((Uint8List b) => ByteData.sublistView(b)),
      );
    }
    await loader.load();
  }

  await load('Nunito Sans', <String>['fonts/NunitoSans.ttf']);
  await load('Permanent Marker', <String>['fonts/PermanentMarker-Regular.ttf']);
  await load('Caveat', <String>['fonts/Caveat-Regular.ttf']);
  await load('Cascadia Mono', <String>['fonts/CascadiaMono-Regular.ttf']);
}
