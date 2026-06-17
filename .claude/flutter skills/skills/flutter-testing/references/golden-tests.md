# Golden Tests in Flutter

Golden tests (also called snapshot tests) compare a widget's rendered output
against a reference image file. If the rendering changes, the test fails
until the golden file is intentionally updated.

---

## matchesGoldenFile Setup

Golden tests use the standard `flutter_test` package. No extra dependencies
are needed for basic goldens.

```dart
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:my_app/features/settings/presentation/settings_page.dart';

void main() {
  testWidgets('SettingsPage matches golden', (tester) async {
    await tester.pumpWidget(
      const MaterialApp(home: SettingsPage()),
    );
    await tester.pumpAndSettle();

    await expectLater(
      find.byType(SettingsPage),
      matchesGoldenFile('goldens/settings_page.png'),
    );
  });
}
```

The golden file path is relative to the test file.

---

## Creating Golden Files

When you write a new golden test, there is no reference image yet. Generate
it by running the test with the `--update-goldens` flag:

```bash
flutter test --update-goldens test/features/settings/presentation/settings_page_test.dart
```

This creates the `.png` file at the path you specified. **Commit the golden
file to version control.**

---

## Updating Goldens

When an intentional UI change causes a golden test to fail:

```bash
# Update all goldens in the project
flutter test --update-goldens

# Update goldens for a specific file
flutter test --update-goldens test/features/settings/presentation/settings_page_test.dart
```

Review the diff of the `.png` file in your git client before committing.

---

## Font Loading for Consistent Results

By default, Flutter tests use the "Ahem" font (a square glyph font) which
makes text appear as blocks. This is intentional for determinism, but
makes golden images hard to review.

### Loading Real Fonts

Create a helper to load your app's fonts before golden tests run:

```dart
// test/helpers/golden_test_helpers.dart
import 'dart:io';
import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';

/// Loads fonts from the asset bundle so golden files render real glyphs.
Future<void> loadAppFonts() async {
  TestWidgetsFlutterBinding.ensureInitialized();

  // Load Roboto (or whatever font your app uses).
  final roboto = File('assets/fonts/Roboto-Regular.ttf');
  if (roboto.existsSync()) {
    final fontLoader = FontLoader('Roboto')
      ..addFont(
        Future.value(
          ByteData.view(roboto.readAsBytesSync().buffer),
        ),
      );
    await fontLoader.load();
  }

  // Load Material Icons.
  final materialIcons = File(
    '${Directory.current.path}/build/unit_test_assets/packages/flutter/lib/src/material/icons/MaterialIcons-Regular.otf',
  );
  if (materialIcons.existsSync()) {
    final fontLoader = FontLoader('MaterialIcons')
      ..addFont(
        Future.value(
          ByteData.view(materialIcons.readAsBytesSync().buffer),
        ),
      );
    await fontLoader.load();
  }
}
```

Call `loadAppFonts()` in a `setUpAll()`:

```dart
void main() {
  setUpAll(loadAppFonts);

  testWidgets('card renders correctly', (tester) async {
    await tester.pumpWidget(
      const MaterialApp(home: ProductCard(product: sampleProduct)),
    );
    await tester.pumpAndSettle();

    await expectLater(
      find.byType(ProductCard),
      matchesGoldenFile('goldens/product_card.png'),
    );
  });
}
```

---

## Platform-Specific Goldens

Golden image output can differ slightly between macOS, Linux, and Windows
due to anti-aliasing, font rendering, and GPU differences. Strategies:

### Strategy 1: Single CI Platform (Recommended)

Pick one platform (e.g., Linux on GitHub Actions) as the source of truth.
Generate and update goldens only on that platform.

```yaml
# .github/workflows/golden_tests.yml
name: Golden Tests

on:
  pull_request:

jobs:
  goldens:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: subosito/flutter-action@v2
        with:
          channel: stable
      - run: flutter test --tags golden
```

Tag golden tests so they can be run selectively:

```dart
@Tags(['golden'])
library;

import 'package:flutter_test/flutter_test.dart';

void main() {
  testWidgets('golden: app bar', (tester) async {
    // ...
  });
}
```

### Strategy 2: Per-Platform Goldens

Store goldens in platform-specific directories:

```dart
import 'dart:io';

String goldenPath(String name) {
  final platform = Platform.operatingSystem; // linux, macos, windows
  return 'goldens/$platform/$name';
}
```

This increases maintenance but allows local golden updates on any OS.

### Strategy 3: Tolerance Threshold

Set a pixel tolerance to allow minor anti-aliasing differences:

```dart
// test/flutter_test_config.dart
import 'dart:async';

import 'package:flutter_test/flutter_test.dart';

Future<void> testExecutable(FutureOr<void> Function() testMain) async {
  goldenFileComparator = _TolerantGoldenComparator(
    Uri.parse('test/'),
    tolerance: 0.005, // 0.5% pixel difference allowed
  );
  await testMain();
}

class _TolerantGoldenComparator extends LocalFileComparator {
  _TolerantGoldenComparator(super.testFile, {required this.tolerance});

  final double tolerance;

  @override
  Future<bool> compare(Uint8List imageBytes, Uri golden) async {
    final result = await GoldenFileComparator.compareLists(
      imageBytes,
      await getGoldenBytes(golden),
    );
    return result.passed || result.diffPercent <= tolerance;
  }
}
```

---

## Golden File CI Strategy

1. **Run goldens only in CI** to avoid cross-platform diffs from developer
   machines. Tag tests and run them selectively.

2. **Pin the Flutter version** in CI. Golden output changes between Flutter
   versions.

3. **Store golden files in version control.** They are small PNGs and must
   be reviewed in PRs.

4. **Use a bot or CI step to auto-update goldens** on a scheduled cadence
   (e.g., after Flutter upgrades) and open a PR for review.

5. **Separate golden test jobs** from unit/widget test jobs so golden
   failures do not block unrelated merges.

---

## Alchemist Package for Advanced Goldens

[Alchemist](https://pub.dev/packages/alchemist) provides a higher-level API
for golden testing with automatic font loading, device-frame simulation, and
theme variants.

### Setup

```yaml
dev_dependencies:
  alchemist: ^0.10.0
```

### GoldenTestGroup and GoldenTestScenario

```dart
import 'package:alchemist/alchemist.dart';
import 'package:flutter/material.dart';
import 'package:my_app/features/buttons/presentation/primary_button.dart';

void main() {
  goldenTest(
    'PrimaryButton variants',
    fileName: 'primary_button_variants',
    builder: () => GoldenTestGroup(
      children: [
        GoldenTestScenario(
          name: 'default',
          child: PrimaryButton(
            label: 'Submit',
            onPressed: () {},
          ),
        ),
        GoldenTestScenario(
          name: 'disabled',
          child: const PrimaryButton(
            label: 'Submit',
            onPressed: null,
          ),
        ),
        GoldenTestScenario(
          name: 'loading',
          child: PrimaryButton(
            label: 'Submit',
            onPressed: () {},
            isLoading: true,
          ),
        ),
      ],
    ),
  );
}
```

### Theme Variants

```dart
goldenTest(
  'ProfileCard in light and dark theme',
  fileName: 'profile_card_themes',
  builder: () => GoldenTestGroup(
    children: [
      GoldenTestScenario(
        name: 'light',
        child: Theme(
          data: ThemeData.light(),
          child: const ProfileCard(name: 'Alice'),
        ),
      ),
      GoldenTestScenario(
        name: 'dark',
        child: Theme(
          data: ThemeData.dark(),
          child: const ProfileCard(name: 'Alice'),
        ),
      ),
    ],
  ),
);
```

### CI-Only Golden Comparison

Alchemist supports a CI mode that skips rendering to screen and only
compares files:

```dart
// test/flutter_test_config.dart
import 'dart:async';

import 'package:alchemist/alchemist.dart';

Future<void> testExecutable(FutureOr<void> Function() testMain) async {
  final isCI = const bool.fromEnvironment('CI', defaultValue: false);
  return AlchemistConfig.runWithConfig(
    config: AlchemistConfig(
      platformGoldensConfig: PlatformGoldensConfig(
        enabled: !isCI,
      ),
      ciGoldensConfig: CiGoldensConfig(
        enabled: isCI,
      ),
    ),
    run: testMain,
  );
}
```

---

## Comparing Goldens Across Devices

For device-frame simulations (e.g., showing how a widget looks on iPhone SE
vs Pixel 7), combine Alchemist with custom surface sizes:

```dart
goldenTest(
  'LoginPage on multiple devices',
  fileName: 'login_page_devices',
  builder: () => GoldenTestGroup(
    columnCount: 2,
    children: [
      GoldenTestScenario(
        name: 'iPhone SE (375x667)',
        constraints: const BoxConstraints(
          maxWidth: 375,
          maxHeight: 667,
        ),
        child: const LoginPage(),
      ),
      GoldenTestScenario(
        name: 'Pixel 7 (412x915)',
        constraints: const BoxConstraints(
          maxWidth: 412,
          maxHeight: 915,
        ),
        child: const LoginPage(),
      ),
      GoldenTestScenario(
        name: 'iPad (810x1080)',
        constraints: const BoxConstraints(
          maxWidth: 810,
          maxHeight: 1080,
        ),
        child: const LoginPage(),
      ),
    ],
  ),
);
```

This generates a single golden image showing the widget at each device size
side by side, making visual review straightforward.
