# Accessibility Testing

## Overview

Testing accessibility ensures that changes do not regress the experience for
assistive technology users. Flutter provides built-in accessibility guidelines
that can be asserted in widget tests, a semantics debugger overlay, and
integration with platform accessibility inspectors.

---

## 1. Accessibility Guideline Checking in Widget Tests

Flutter ships four built-in guideline matchers:

| Guideline                        | What It Checks                                                       |
|----------------------------------|----------------------------------------------------------------------|
| `androidTapTargetGuideline`      | Tap targets are at least 48x48 dp.                                   |
| `iOSTapTargetGuideline`          | Tap targets are at least 44x44 dp.                                   |
| `textContrastGuideline`          | Text meets WCAG AA contrast ratios (4.5:1 normal, 3:1 large).        |
| `labeledTapTargetGuideline`      | Every tap target has a semantic label.                                |

### Basic Test

```dart
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  testWidgets('Home screen passes accessibility guidelines', (tester) async {
    final SemanticsHandle handle = tester.ensureSemantics();

    await tester.pumpWidget(
      const MaterialApp(
        home: HomeScreen(),
      ),
    );

    // Check all four guidelines.
    await expectLater(tester, meetsGuideline(androidTapTargetGuideline));
    await expectLater(tester, meetsGuideline(iOSTapTargetGuideline));
    await expectLater(tester, meetsGuideline(textContrastGuideline));
    await expectLater(tester, meetsGuideline(labeledTapTargetGuideline));

    handle.dispose();
  });
}

class HomeScreen extends StatelessWidget {
  const HomeScreen({super.key});

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Home')),
      body: Center(
        child: ElevatedButton(
          onPressed: () {},
          child: const Text('Get Started'),
        ),
      ),
    );
  }
}
```

### Testing a Single Widget in Isolation

```dart
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  testWidgets('IconButton has a semantic label', (tester) async {
    final handle = tester.ensureSemantics();

    await tester.pumpWidget(
      MaterialApp(
        home: Scaffold(
          body: IconButton(
            icon: const Icon(Icons.favorite),
            tooltip: 'Add to favorites',
            onPressed: () {},
          ),
        ),
      ),
    );

    await expectLater(tester, meetsGuideline(labeledTapTargetGuideline));

    handle.dispose();
  });
}
```

---

## 2. Flutter Test with Semantics

### SemanticsHandle Setup

The `SemanticsHandle` enables the semantic tree in tests. Without it, semantic
queries return empty results.

```dart
import 'package:flutter_test/flutter_test.dart';

void main() {
  testWidgets('example with semantics', (tester) async {
    // MUST call ensureSemantics() before pumping any widget.
    final SemanticsHandle handle = tester.ensureSemantics();

    // ... pump widget and run assertions ...

    // MUST dispose when done.
    handle.dispose();
  });
}
```

### Querying the Semantic Tree

```dart
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  testWidgets('cart badge announces item count', (tester) async {
    final handle = tester.ensureSemantics();

    await tester.pumpWidget(
      const MaterialApp(
        home: Scaffold(
          body: CartBadge(itemCount: 5),
        ),
      ),
    );

    final semantics = tester.getSemantics(find.byType(CartBadge));
    expect(semantics.label, '5 items in cart');
    expect(semantics.hasFlag(SemanticsFlag.isLiveRegion), isTrue);

    handle.dispose();
  });
}

class CartBadge extends StatelessWidget {
  final int itemCount;
  const CartBadge({super.key, required this.itemCount});

  @override
  Widget build(BuildContext context) {
    return Semantics(
      liveRegion: true,
      label: '$itemCount items in cart',
      child: Badge(
        label: Text('$itemCount'),
        child: const Icon(Icons.shopping_cart),
      ),
    );
  }
}
```

### Asserting Semantic Flags and Properties

```dart
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  testWidgets('submit button has correct semantics', (tester) async {
    final handle = tester.ensureSemantics();

    await tester.pumpWidget(
      MaterialApp(
        home: Scaffold(
          body: Semantics(
            button: true,
            enabled: true,
            label: 'Submit order',
            child: ElevatedButton(
              onPressed: () {},
              child: const Text('Submit'),
            ),
          ),
        ),
      ),
    );

    final semantics = tester.getSemantics(find.text('Submit'));
    expect(semantics.hasFlag(SemanticsFlag.isButton), isTrue);
    expect(semantics.hasFlag(SemanticsFlag.isEnabled), isTrue);

    handle.dispose();
  });
}
```

### Testing Custom Semantic Actions

```dart
import 'package:flutter/material.dart';
import 'package:flutter/semantics.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  testWidgets('message card exposes archive action', (tester) async {
    final handle = tester.ensureSemantics();
    bool archived = false;

    await tester.pumpWidget(
      MaterialApp(
        home: Scaffold(
          body: Semantics(
            label: 'Message from Alice',
            customSemanticsActions: <CustomSemanticsAction, VoidCallback>{
              const CustomSemanticsAction(label: 'Archive'): () {
                archived = true;
              },
            },
            child: const ListTile(title: Text('Alice')),
          ),
        ),
      ),
    );

    // Verify the custom action is present in the semantic tree.
    final node = tester.getSemantics(find.byType(ListTile));
    final actions = node.getSemanticsData().customSemanticsActionIds;
    expect(actions, isNotNull);
    expect(actions, isNotEmpty);

    handle.dispose();
  });
}
```

---

## 3. Accessibility Inspector in DevTools

### Opening the Inspector

1. Run your app in debug mode: `flutter run`.
2. Open the DevTools URL printed in the terminal.
3. Navigate to the **Inspector** tab.
4. Toggle **Accessibility** in the bottom toolbar.

### What It Shows

- Semantic node boundaries overlaid on the widget tree.
- Semantic labels, roles, values, and hints for each node.
- Warnings for missing labels or small tap targets.

### Using the Semantics Debugger Overlay

```dart
import 'package:flutter/material.dart';

void main() {
  runApp(
    MaterialApp(
      showSemanticsDebugger: true,
      home: const MyApp(),
    ),
  );
}

class MyApp extends StatelessWidget {
  const MyApp({super.key});

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Debug')),
      body: const Center(child: Text('Hello')),
    );
  }
}
```

The overlay replaces the rendered UI with a text view of the semantic tree,
showing labels, roles, and tree structure.

---

## 4. Manual Testing with VoiceOver and TalkBack

### VoiceOver (iOS / macOS)

**On Device:**

1. Build and install: `flutter run --release` on a connected iOS device.
2. Open **Settings > Accessibility > VoiceOver** and enable it.
3. Navigate by swiping right (next element) and left (previous element).
4. Double-tap to activate the focused element.
5. Use the rotor (two-finger twist) to access custom actions, headings, and
   links.

**On Simulator:**

1. Run the iOS simulator.
2. Open **System Preferences > Accessibility > VoiceOver** on macOS.
3. Enable VoiceOver (Cmd + F5).
4. Use Ctrl + Option + arrow keys to navigate.

**Checklist:**

- [ ] Every interactive element is announced with a meaningful label.
- [ ] Headings are announced as "heading" after the label.
- [ ] Buttons are announced as "button."
- [ ] Images have descriptive labels or are excluded.
- [ ] Focus order matches visual reading order.
- [ ] Dynamic changes are announced via live regions.

### TalkBack (Android)

**On Device:**

1. Build and install: `flutter run --release` on a connected Android device.
2. Open **Settings > Accessibility > TalkBack** and enable it.
3. Swipe right/left to move between elements.
4. Double-tap to activate.
5. Swipe up then right to open the local context menu for custom actions.

**On Emulator:**

1. Open the Android emulator.
2. Go to **Settings > Accessibility > TalkBack** and enable it.
3. Use the same gestures as on device; mouse clicks act as taps.

**Checklist:**

- [ ] All interactive widgets are reachable via swipe navigation.
- [ ] Roles (button, checkbox, slider) are announced correctly.
- [ ] Form errors are announced when they appear.
- [ ] No dead-end focus traps exist.
- [ ] Custom actions appear in the local context menu.

---

## 5. Color Contrast Checking Tools

### In-Code Contrast Calculation

```dart
import 'dart:math' as math;
import 'package:flutter/material.dart';

double luminance(Color color) {
  double linearize(double channel) {
    return channel <= 0.03928
        ? channel / 12.92
        : math.pow((channel + 0.055) / 1.055, 2.4).toDouble();
  }

  final r = linearize(color.r);
  final g = linearize(color.g);
  final b = linearize(color.b);
  return 0.2126 * r + 0.7152 * g + 0.0722 * b;
}

double contrastRatio(Color foreground, Color background) {
  final l1 = luminance(foreground) + 0.05;
  final l2 = luminance(background) + 0.05;
  return l1 > l2 ? l1 / l2 : l2 / l1;
}

bool meetsWcagAA(Color foreground, Color background, {bool isLargeText = false}) {
  final ratio = contrastRatio(foreground, background);
  return isLargeText ? ratio >= 3.0 : ratio >= 4.5;
}
```

### Using the textContrastGuideline in Tests

```dart
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  testWidgets('text has sufficient contrast', (tester) async {
    final handle = tester.ensureSemantics();

    await tester.pumpWidget(
      const MaterialApp(
        home: Scaffold(
          backgroundColor: Colors.white,
          body: Center(
            child: Text(
              'High contrast text',
              style: TextStyle(color: Colors.black87, fontSize: 16),
            ),
          ),
        ),
      ),
    );

    await expectLater(tester, meetsGuideline(textContrastGuideline));

    handle.dispose();
  });
}
```

### External Tools

| Tool                              | Platform       | URL / Access                                  |
|-----------------------------------|----------------|-----------------------------------------------|
| WebAIM Contrast Checker           | Web            | https://webaim.org/resources/contrastchecker/ |
| Colour Contrast Analyser (CCA)    | macOS, Windows | https://www.tpgi.com/color-contrast-checker/  |
| Accessibility Scanner             | Android        | Play Store (Google)                           |
| Xcode Accessibility Inspector     | macOS / iOS    | Xcode > Open Developer Tool > Accessibility   |
| Flutter DevTools Inspector         | Cross-platform | Built into `flutter run --debug`              |

---

## 6. SemanticsHandle for Test Setup

Every accessibility test must activate the semantics layer. Failing to do so
results in empty semantic nodes.

### Pattern: setUp and tearDown

```dart
import 'package:flutter_test/flutter_test.dart';

void main() {
  late SemanticsHandle handle;

  setUp(() {
    // Note: ensureSemantics must be called inside a testWidgets callback
    // or via TestWidgetsFlutterBinding. The pattern below shows the
    // recommended per-test approach.
  });

  testWidgets('accessible widget test', (tester) async {
    handle = tester.ensureSemantics();

    // ... pump widget, run assertions ...

    handle.dispose();
  });
}
```

### Helper Function for Repeated Tests

```dart
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

/// Pumps the [widget] inside a MaterialApp and asserts all accessibility
/// guidelines. Returns the [SemanticsHandle] for further queries.
Future<SemanticsHandle> pumpAndCheckAccessibility(
  WidgetTester tester,
  Widget widget,
) async {
  final handle = tester.ensureSemantics();

  await tester.pumpWidget(MaterialApp(home: widget));

  await expectLater(tester, meetsGuideline(androidTapTargetGuideline));
  await expectLater(tester, meetsGuideline(iOSTapTargetGuideline));
  await expectLater(tester, meetsGuideline(textContrastGuideline));
  await expectLater(tester, meetsGuideline(labeledTapTargetGuideline));

  return handle;
}

void main() {
  testWidgets('login screen is accessible', (tester) async {
    final handle = await pumpAndCheckAccessibility(
      tester,
      Scaffold(
        body: Column(
          children: [
            const TextField(decoration: InputDecoration(labelText: 'Email')),
            const SizedBox(height: 16),
            const TextField(
              decoration: InputDecoration(labelText: 'Password'),
              obscureText: true,
            ),
            const SizedBox(height: 24),
            ElevatedButton(
              onPressed: () {},
              child: const Text('Log In'),
            ),
          ],
        ),
      ),
    );

    handle.dispose();
  });
}
```

---

## 7. Common Accessibility Failures and Fixes

### Failure: Missing Semantic Label on IconButton

```
Expected: meets guideline labeledTapTargetGuideline
  Actual: <WidgetTester>
  Which: found 1 tap target(s) without a label
```

**Fix:** Add a `tooltip` (which doubles as the semantic label):

```dart
IconButton(
  icon: const Icon(Icons.menu),
  tooltip: 'Open navigation menu',
  onPressed: () {},
)
```

### Failure: Tap Target Too Small

```
Expected: meets guideline androidTapTargetGuideline
  Actual: <WidgetTester>
  Which: found 1 tap target(s) with size below 48x48
```

**Fix:** Wrap in a `SizedBox` or use `minimumSize` on the button style:

```dart
SizedBox(
  width: 48,
  height: 48,
  child: IconButton(
    icon: const Icon(Icons.close),
    tooltip: 'Close',
    onPressed: () {},
  ),
)
```

Or use padding:

```dart
IconButton(
  icon: const Icon(Icons.close),
  tooltip: 'Close',
  padding: const EdgeInsets.all(12),
  constraints: const BoxConstraints(minWidth: 48, minHeight: 48),
  onPressed: () {},
)
```

### Failure: Insufficient Color Contrast

```
Expected: meets guideline textContrastGuideline
  Actual: <WidgetTester>
  Which: found 1 text(s) with insufficient contrast ratio
```

**Fix:** Darken the text color or lighten the background:

```dart
// Before (fails):
Text('Hello', style: TextStyle(color: Colors.grey.shade400))

// After (passes):
Text('Hello', style: TextStyle(color: Colors.grey.shade800))
```

### Failure: Decorative Image Read by Screen Reader

**Symptom:** VoiceOver announces "Image" with no useful description.

**Fix:** Exclude it from semantics:

```dart
Image.asset(
  'assets/decoration.png',
  excludeFromSemantics: true,
)
```

### Failure: Focus Trapped in a Dismissed Dialog

**Symptom:** After closing a dialog, pressing Tab does nothing or focus
jumps to an unexpected widget.

**Fix:** Restore focus to the trigger widget after the dialog closes:

```dart
Future<void> _showDialog() async {
  await showDialog<void>(
    context: context,
    builder: (_) => const AlertDialog(title: Text('Info')),
  );
  _triggerFocusNode.requestFocus(); // Restore focus.
}
```

### Failure: Dynamic Content Not Announced

**Symptom:** A counter changes on screen but TalkBack/VoiceOver stays silent.

**Fix:** Mark the container as a live region:

```dart
Semantics(
  liveRegion: true,
  label: '$count new notifications',
  child: Text('$count'),
)
```

Or use `SemanticsService.announce` for one-shot messages:

```dart
import 'package:flutter/semantics.dart';

SemanticsService.announce('Item added to cart', TextDirection.ltr);
```

---

## 8. Automated CI Checks for Accessibility

### Adding Accessibility Tests to Existing Test Suites

Create a dedicated test file per screen:

```
test/
  accessibility/
    home_screen_a11y_test.dart
    settings_screen_a11y_test.dart
    ...
```

```dart
// test/accessibility/home_screen_a11y_test.dart
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:my_app/screens/home_screen.dart';

void main() {
  group('HomeScreen accessibility', () {
    testWidgets('passes all guidelines', (tester) async {
      final handle = tester.ensureSemantics();

      await tester.pumpWidget(
        const MaterialApp(home: HomeScreen()),
      );

      await expectLater(tester, meetsGuideline(androidTapTargetGuideline));
      await expectLater(tester, meetsGuideline(iOSTapTargetGuideline));
      await expectLater(tester, meetsGuideline(textContrastGuideline));
      await expectLater(tester, meetsGuideline(labeledTapTargetGuideline));

      handle.dispose();
    });

    testWidgets('heading semantics are correct', (tester) async {
      final handle = tester.ensureSemantics();

      await tester.pumpWidget(
        const MaterialApp(home: HomeScreen()),
      );

      final heading = tester.getSemantics(find.text('Welcome'));
      expect(heading.hasFlag(SemanticsFlag.isHeader), isTrue);

      handle.dispose();
    });
  });
}
```

### GitHub Actions Workflow

```yaml
# .github/workflows/accessibility.yml
name: Accessibility Tests

on:
  pull_request:
    branches: [main]
  push:
    branches: [main]

jobs:
  a11y-tests:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - uses: subosito/flutter-action@v2
        with:
          flutter-version: "3.x"
          channel: stable

      - name: Install dependencies
        run: flutter pub get

      - name: Run accessibility tests
        run: flutter test test/accessibility/ --reporter=expanded

      - name: Run all tests (includes a11y)
        run: flutter test --reporter=expanded
```

### GitLab CI Configuration

```yaml
# .gitlab-ci.yml
accessibility-tests:
  image: ghcr.io/cirruslabs/flutter:stable
  stage: test
  script:
    - flutter pub get
    - flutter test test/accessibility/ --reporter=expanded
  rules:
    - if: $CI_PIPELINE_SOURCE == "merge_request_event"
    - if: $CI_COMMIT_BRANCH == "main"
```

### Pre-Commit Hook (Optional)

```bash
#!/usr/bin/env bash
# .git/hooks/pre-commit

echo "Running accessibility tests..."
flutter test test/accessibility/ --reporter=compact
if [ $? -ne 0 ]; then
  echo "Accessibility tests failed. Fix issues before committing."
  exit 1
fi
```

### Test Coverage Strategy

| Layer              | Tool                               | What It Catches                                     |
|--------------------|-------------------------------------|-----------------------------------------------------|
| Unit / Widget      | `meetsGuideline` matchers           | Tap target size, contrast, labels                   |
| Integration        | `flutter drive` + semantics queries | End-to-end focus flow, screen transitions           |
| Manual             | VoiceOver / TalkBack                | Announcement quality, gesture usability, rotor items|
| Static Analysis    | Custom lint rules                   | Missing `tooltip` on `IconButton`, missing labels   |
| Continuous (CI)    | GitHub Actions / GitLab CI          | Prevents regressions on every pull request          |

By combining automated guideline checks in CI with periodic manual testing on
real devices, you build a robust accessibility quality gate that catches
regressions before they reach users.
