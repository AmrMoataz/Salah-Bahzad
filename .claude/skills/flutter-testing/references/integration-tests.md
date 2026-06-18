# Integration Testing in Flutter

## Package Setup

Add the `integration_test` package as a dev dependency. It ships with the
Flutter SDK so no version number is needed.

```yaml
# pubspec.yaml
dev_dependencies:
  flutter_test:
    sdk: flutter
  integration_test:
    sdk: flutter
```

Create an `integration_test/` directory at the project root (next to `lib/`
and `test/`).

```
my_app/
  integration_test/
    app_test.dart
    robots/
      login_robot.dart
      home_robot.dart
  lib/
  test/
```

---

## IntegrationTestWidgetsFlutterBinding

Every integration test file must initialise the binding before running any
tests.

```dart
// integration_test/app_test.dart
import 'package:flutter_test/flutter_test.dart';
import 'package:integration_test/integration_test.dart';
import 'package:my_app/main.dart' as app;

void main() {
  IntegrationTestWidgetsFlutterBinding.ensureInitialized();

  testWidgets('full login flow', (tester) async {
    app.main();
    await tester.pumpAndSettle();

    // ... interact with the live app
  });
}
```

---

## Full App Test Flows

### Robot Pattern

Use the robot (page-object) pattern to keep integration tests readable and
to avoid duplicating finder logic.

```dart
// integration_test/robots/login_robot.dart
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

class LoginRobot {
  LoginRobot(this.tester);

  final WidgetTester tester;

  Future<void> enterEmail(String email) async {
    await tester.enterText(
      find.byKey(const Key('login-email')),
      email,
    );
  }

  Future<void> enterPassword(String password) async {
    await tester.enterText(
      find.byKey(const Key('login-password')),
      password,
    );
  }

  Future<void> tapSignIn() async {
    await tester.tap(find.byKey(const Key('login-submit')));
    await tester.pumpAndSettle();
  }

  void expectErrorMessage(String message) {
    expect(find.text(message), findsOneWidget);
  }
}
```

```dart
// integration_test/robots/home_robot.dart
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

class HomeRobot {
  HomeRobot(this.tester);

  final WidgetTester tester;

  void expectWelcomeText(String name) {
    expect(find.textContaining('Welcome, $name'), findsOneWidget);
  }

  Future<void> tapLogout() async {
    await tester.tap(find.byKey(const Key('logout-button')));
    await tester.pumpAndSettle();
  }
}
```

### Composing Robots in a Test

```dart
// integration_test/app_test.dart
import 'package:flutter_test/flutter_test.dart';
import 'package:integration_test/integration_test.dart';
import 'package:my_app/main.dart' as app;

import 'robots/home_robot.dart';
import 'robots/login_robot.dart';

void main() {
  IntegrationTestWidgetsFlutterBinding.ensureInitialized();

  group('Authentication flow', () {
    testWidgets('user can sign in and see the home page', (tester) async {
      app.main();
      await tester.pumpAndSettle();

      final login = LoginRobot(tester);
      await login.enterEmail('user@example.com');
      await login.enterPassword('s3cret!Pass');
      await login.tapSignIn();

      final home = HomeRobot(tester);
      home.expectWelcomeText('Test User');
    });

    testWidgets('shows error on invalid credentials', (tester) async {
      app.main();
      await tester.pumpAndSettle();

      final login = LoginRobot(tester);
      await login.enterEmail('wrong@example.com');
      await login.enterPassword('bad');
      await login.tapSignIn();

      login.expectErrorMessage('Invalid email or password');
    });
  });
}
```

---

## Testing Navigation Flows

```dart
testWidgets('navigates from product list to product detail', (tester) async {
  app.main();
  await tester.pumpAndSettle();

  // We start on the product list page.
  expect(find.text('Products'), findsOneWidget);

  // Tap the first product.
  await tester.tap(find.text('Wireless Mouse'));
  await tester.pumpAndSettle();

  // Verify we are on the detail page.
  expect(find.text('Wireless Mouse'), findsOneWidget);
  expect(find.text('\$29.99'), findsOneWidget);

  // Go back.
  await tester.pageBack();
  await tester.pumpAndSettle();

  expect(find.text('Products'), findsOneWidget);
});
```

---

## Testing with Real Services vs Mocks

Integration tests can run against real backends or in-memory fakes. Choose
based on what you are validating.

### Using Real Services

```dart
testWidgets('fetches data from staging API', (tester) async {
  // The app reads the base URL from an environment variable.
  app.main(environment: Environment.staging);
  await tester.pumpAndSettle();

  expect(find.byType(ProductCard), findsWidgets);
});
```

### Using Mock / Fake Services

Register fakes in a helper so that the app boots with deterministic data.

```dart
// integration_test/helpers/test_app.dart
import 'package:my_app/app/admin_application.dart';
import 'package:my_app/app/di_configuration/configure.dart';

import 'fake_product_repository.dart';

Future<AdminApplication> buildTestApp() async {
  await getIt.reset();
  getIt.registerLazySingleton<ProductRepository>(() => FakeProductRepository());
  return const AdminApplication();
}
```

```dart
// integration_test/helpers/fake_product_repository.dart
class FakeProductRepository implements ProductRepository {
  @override
  Future<List<Product>> getProducts() async {
    return const [
      Product(id: '1', title: 'Fake Laptop', price: 999.99),
      Product(id: '2', title: 'Fake Phone', price: 699.99),
    ];
  }

  @override
  Future<Product> getProduct(String id) async {
    final products = await getProducts();
    return products.firstWhere((p) => p.id == id);
  }
}
```

```dart
testWidgets('displays fake products', (tester) async {
  final appWidget = await buildTestApp();
  await tester.pumpWidget(appWidget);
  await tester.pumpAndSettle();

  expect(find.text('Fake Laptop'), findsOneWidget);
  expect(find.text('Fake Phone'), findsOneWidget);
});
```

---

## Screenshot Capture in Tests

The `IntegrationTestWidgetsFlutterBinding` provides a method to take
screenshots during a test. This is useful for visual regression or
documentation.

```dart
void main() {
  final binding = IntegrationTestWidgetsFlutterBinding.ensureInitialized();

  testWidgets('capture screenshots during checkout flow', (tester) async {
    app.main();
    await tester.pumpAndSettle();

    await binding.convertFlutterSurfaceToImage();

    // Screenshot 1: product list
    await tester.pumpAndSettle();
    await binding.takeScreenshot('01_product_list');

    // Add to cart
    await tester.tap(find.byKey(const Key('add-to-cart-1')));
    await tester.pumpAndSettle();
    await binding.takeScreenshot('02_item_added');

    // Navigate to cart
    await tester.tap(find.byIcon(Icons.shopping_cart));
    await tester.pumpAndSettle();
    await binding.takeScreenshot('03_cart_page');
  });
}
```

Screenshots are saved to the device or emulator. On Android, they go to the
test results directory; on iOS, to the derived data folder.

---

## Running on Devices and Emulators

```bash
# Run all integration tests on a connected device
flutter test integration_test

# Run a specific test file
flutter test integration_test/app_test.dart

# Run on a specific device
flutter test integration_test --device-id <device-id>

# Run on Chrome (web)
flutter test integration_test --device-id chrome

# List connected devices
flutter devices
```

### Running on Multiple Devices

```bash
# Run on all connected devices in parallel
flutter devices | tail -n +2 | while read -r line; do
  DEVICE_ID=$(echo "$line" | awk '{print $NF}')
  flutter test integration_test --device-id "$DEVICE_ID" &
done
wait
```

---

## CI Configuration for Integration Tests

### GitHub Actions

```yaml
# .github/workflows/integration_tests.yml
name: Integration Tests

on:
  push:
    branches: [main]
  pull_request:

jobs:
  integration-test-android:
    runs-on: ubuntu-latest
    strategy:
      matrix:
        api-level: [30, 33]
    steps:
      - uses: actions/checkout@v4
      - uses: subosito/flutter-action@v2
        with:
          channel: stable
      - name: Enable KVM
        run: |
          echo 'KERNEL=="kvm", GROUP="kvm", MODE="0666", OPTIONS+="static_node=kvm"' \
            | sudo tee /etc/udev/rules.d/99-kvm4all.rules
          sudo udevadm control --reload-rules
          sudo udevadm trigger --name-match=kvm
      - name: Run integration tests
        uses: reactivecircus/android-emulator-runner@v2
        with:
          api-level: ${{ matrix.api-level }}
          arch: x86_64
          script: flutter test integration_test --flavor staging

  integration-test-ios:
    runs-on: macos-latest
    steps:
      - uses: actions/checkout@v4
      - uses: subosito/flutter-action@v2
        with:
          channel: stable
      - name: Boot iOS Simulator
        run: |
          DEVICE=$(xcrun simctl list devices available | grep "iPhone" | head -1 | sed 's/.*(\(.*\)).*/\1/')
          xcrun simctl boot "$DEVICE"
      - name: Run integration tests
        run: flutter test integration_test

  integration-test-web:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: subosito/flutter-action@v2
        with:
          channel: stable
      - name: Install ChromeDriver
        uses: nanasess/setup-chromedriver@v2
      - name: Run integration tests on Chrome
        run: |
          chromedriver --port=4444 &
          flutter test integration_test --device-id chrome
```

---

## Performance Profiling in Integration Tests

Use the `traceAction` API to capture a performance timeline during a user
interaction and then assert against the results.

```dart
void main() {
  final binding = IntegrationTestWidgetsFlutterBinding.ensureInitialized();

  testWidgets('scrolling performance', (tester) async {
    app.main();
    await tester.pumpAndSettle();

    // Record a timeline while scrolling.
    await binding.traceAction(
      () async {
        final list = find.byType(ListView);
        for (var i = 0; i < 5; i++) {
          await tester.fling(list, const Offset(0, -300), 1000);
          await tester.pumpAndSettle();
        }
      },
      reportKey: 'scrolling_timeline',
    );
  });
}
```

Run with `--profile` for more accurate timing:

```bash
flutter test integration_test/performance_test.dart --profile
```

The timeline JSON is written to the test output directory and can be
analysed with Chrome's `chrome://tracing` tool or Dart DevTools.

### Asserting on Frame Metrics

```dart
testWidgets('maintains 60fps during scroll', (tester) async {
  app.main();
  await tester.pumpAndSettle();

  final timeline = await binding.traceAction(
    () async {
      await tester.fling(find.byType(ListView), const Offset(0, -500), 2000);
      await tester.pumpAndSettle();
    },
    reportKey: 'scroll_perf',
  );

  // The binding reports summary metrics.
  // In CI, you can parse the JSON and assert thresholds.
  // For local development, simply inspect the output.
});
```

---

## Best Practices

1. **Keep integration tests focused.** Each test should cover one user journey
   (e.g., login flow, checkout flow). Do not combine unrelated flows.

2. **Use the robot pattern.** Encapsulate page interactions in robot classes
   to keep tests readable and reduce maintenance when the UI changes.

3. **Prefer fakes over mocks for integration tests.** Fakes provide
   deterministic data without network calls and are simpler to maintain
   than mock setups.

4. **Tag long-running tests.** Use `@Tags(['integration'])` so you can
   skip them during local development:
   ```bash
   flutter test --exclude-tags integration
   ```

5. **Isolate state between tests.** If tests share a database or
   preferences, reset the state in `setUp` or use separate user accounts.

6. **Run on CI with real devices or emulators.** Do not rely solely on
   `flutter test` in headless mode for integration tests; some issues
   only manifest on actual rendering surfaces.
