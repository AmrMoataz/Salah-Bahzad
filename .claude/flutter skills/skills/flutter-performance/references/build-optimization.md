# Build Size and Startup Optimization

## Analyzing App Size

Flutter provides built-in tooling to understand what contributes to your
binary size.

### --analyze-size Flag

```bash
# Android APK analysis
flutter build apk --analyze-size

# Android App Bundle analysis
flutter build appbundle --analyze-size

# iOS analysis
flutter build ios --analyze-size

# macOS analysis
flutter build macos --analyze-size
```

This generates a JSON snapshot and opens a human-readable summary:

```
app-release.apk (total compressed)                      14.3 MB
  assets/                                                 2.1 MB
    flutter_assets/                                       1.8 MB
      FontManifest.json                                   0.3 KB
      AssetManifest.json                                  0.5 KB
      fonts/                                              1.2 MB
      images/                                             0.6 MB
  lib/                                                   10.1 MB
    arm64-v8a/                                            5.2 MB
      libflutter.so                                       3.8 MB
      libapp.so                                           1.4 MB
    armeabi-v7a/                                          4.9 MB
  res/                                                    0.8 MB
  classes.dex                                             1.3 MB
```

### DevTools App Size Analysis

Open the generated JSON in DevTools for an interactive treemap:

```bash
# The --analyze-size flag prints the path to the JSON file
# Open it in DevTools
dart devtools --app-size-base=apk-code-size-analysis_01.json
```

### Comparing Two Builds

Track size regressions by diffing two analysis snapshots:

```bash
# Build baseline
flutter build apk --analyze-size --target-platform android-arm64
mv build/apk-code-size-analysis_*.json baseline.json

# Make changes, rebuild
flutter build apk --analyze-size --target-platform android-arm64
mv build/apk-code-size-analysis_*.json current.json

# Open diff in DevTools
dart devtools --app-size-base=baseline.json --app-size-diff=current.json
```

## Tree Shaking

Dart's AOT compiler tree-shakes unused code -- but only if the code is
actually unreachable. Common patterns that defeat tree shaking:

### Avoid Barrel File Re-Exports

```dart
// BAD: barrel file re-exports everything
// lib/models/models.dart
export 'user.dart';
export 'product.dart';
export 'order.dart';
export 'analytics_event.dart';  // 5,000 line file, rarely used

// Importing the barrel pulls in all transitive dependencies
import 'package:my_app/models/models.dart';
```

```dart
// GOOD: import only what you need
import 'package:my_app/models/user.dart';
import 'package:my_app/models/product.dart';
```

### show/hide Directive

```dart
// GOOD: import only specific symbols
import 'package:intl/intl.dart' show DateFormat, NumberFormat;

// GOOD: hide large unused symbols
import 'package:some_package/some_package.dart' hide HeavyWidget;
```

### Conditional Imports for Platform-Specific Code

```dart
// Only include web-specific code when compiling for web
import 'stub_platform.dart'
    if (dart.library.html) 'web_platform.dart'
    if (dart.library.io) 'native_platform.dart';
```

### Avoid Reflective Patterns

`dart:mirrors` prevents tree shaking entirely. Never use it. JSON
serialization should use code generation (`json_serializable`,
`freezed`) instead:

```dart
// GOOD: generated code is tree-shakeable
import 'package:json_annotation/json_annotation.dart';

part 'product.g.dart';

@JsonSerializable()
class Product {
  const Product({
    required this.id,
    required this.name,
    required this.price,
  });

  factory Product.fromJson(Map<String, dynamic> json) =>
      _$ProductFromJson(json);

  final String id;
  final String name;
  final double price;

  Map<String, dynamic> toJson() => _$ProductToJson(this);
}
```

## Deferred Loading

Deferred loading (also called lazy loading) lets you load Dart libraries on
demand rather than at startup. This reduces initial bundle size and startup
time.

### Basic Deferred Import

```dart
// Use `deferred as` to mark a library for lazy loading
import 'package:my_app/features/analytics/analytics_dashboard.dart'
    deferred as analytics;

class NavigationService {
  Future<Widget> loadAnalyticsDashboard() async {
    // Downloads and loads the library
    await analytics.loadLibrary();

    // Now the library's symbols are available
    return analytics.AnalyticsDashboard();
  }
}
```

### Deferred Loading in Routes

```dart
import 'package:flutter/material.dart';

import 'package:my_app/features/settings/settings_page.dart'
    deferred as settings;
import 'package:my_app/features/profile/profile_page.dart'
    deferred as profile;

final router = GoRouter(
  routes: [
    GoRoute(
      path: '/',
      builder: (context, state) => const HomePage(),
    ),
    GoRoute(
      path: '/settings',
      builder: (context, state) => _DeferredPage(
        loader: settings.loadLibrary,
        builder: () => settings.SettingsPage(),
      ),
    ),
    GoRoute(
      path: '/profile',
      builder: (context, state) => _DeferredPage(
        loader: profile.loadLibrary,
        builder: () => profile.ProfilePage(),
      ),
    ),
  ],
);

/// Generic widget for loading deferred libraries with a loading indicator.
class _DeferredPage extends StatelessWidget {
  const _DeferredPage({
    required this.loader,
    required this.builder,
  });

  final Future<void> Function() loader;
  final Widget Function() builder;

  @override
  Widget build(BuildContext context) {
    return FutureBuilder<void>(
      future: loader(),
      builder: (context, snapshot) {
        if (snapshot.connectionState != ConnectionState.done) {
          return const Scaffold(
            body: Center(child: CircularProgressIndicator()),
          );
        }

        if (snapshot.hasError) {
          return Scaffold(
            body: Center(
              child: Column(
                mainAxisSize: MainAxisSize.min,
                children: [
                  const Icon(Icons.error_outline, size: 48),
                  const SizedBox(height: 16),
                  Text('Failed to load: ${snapshot.error}'),
                  const SizedBox(height: 16),
                  ElevatedButton(
                    onPressed: () {
                      // Trigger rebuild to retry
                      (context as Element).markNeedsBuild();
                    },
                    child: const Text('Retry'),
                  ),
                ],
              ),
            ),
          );
        }

        return builder();
      },
    );
  }
}
```

### Deferred Loading Considerations

| Consideration | Detail |
|---|---|
| Web-only splitting | On web, deferred loading creates separate JS chunks. On mobile, it has no binary size effect (AOT compiles all code). |
| Network required | On web, `loadLibrary()` may require a network request. Handle errors gracefully. |
| No type access before load | You cannot reference types from a deferred library before calling `loadLibrary()`. |
| Prefixed access | All symbols must be accessed via the prefix: `analytics.SomeClass()`. |
| Testing | In tests, call `loadLibrary()` in `setUp()` to ensure the library is loaded. |

## Code Splitting

Beyond deferred loading, organize code to minimize what ships in the initial
payload:

### Feature-Based Package Structure

```
lib/
  core/               # Shared utilities, always loaded
    network/
    storage/
    theme/
  features/
    auth/             # Small, loaded at startup
    home/             # Small, loaded at startup
    analytics/        # Large, deferred
    admin/            # Large, deferred
    onboarding/       # Medium, deferred after first run
```

### Separate Packages for Heavy Features

For very large features, extract them into separate packages:

```yaml
# pubspec.yaml
dependencies:
  my_app_core: ^1.0.0
  my_app_analytics:
    path: packages/analytics  # can be deferred
  my_app_media:
    path: packages/media      # can be deferred
```

## Minimizing Native Dependencies

Each native plugin adds binary size. Audit regularly:

```bash
# List all direct and transitive dependencies
flutter pub deps --style=compact

# Check for unused dependencies
dart pub outdated
```

### Dependency Audit Checklist

| Check | Action |
|---|---|
| Unused packages in `pubspec.yaml` | Remove them |
| Packages used only in dev/test | Move to `dev_dependencies` |
| Large packages with small usage | Consider replacing with focused alternatives or hand-written code |
| Packages that pull in native code | Check if a Dart-only alternative exists |
| Multiple packages for the same purpose | Consolidate to one |

## Asset Optimization

### Image Compression

Compress images before adding them to the project:

```bash
# PNG optimization
pngquant --quality=65-80 --strip assets/images/*.png

# JPEG optimization
jpegoptim --max=80 --strip-all assets/images/*.jpg

# WebP conversion (smaller than PNG/JPEG)
cwebp -q 80 input.png -o output.webp
```

Use resolution-aware assets to avoid shipping unnecessarily large images:

```
assets/
  images/
    logo.png          # 1x (baseline)
    2.0x/logo.png     # 2x
    3.0x/logo.png     # 3x
```

Flutter automatically selects the appropriate resolution at runtime.

### Font Subsetting

Flutter tree-shakes font glyphs by default. To ensure this works:

```yaml
# pubspec.yaml
flutter:
  fonts:
    - family: Roboto
      fonts:
        - asset: assets/fonts/Roboto-Regular.ttf
        - asset: assets/fonts/Roboto-Bold.ttf
          weight: 700
```

If using icon fonts, only the icons referenced in code are included. Verify
with `--analyze-size`.

For custom icon sets, use SVGs with `flutter_svg` instead of icon fonts to
avoid shipping unused glyphs:

```dart
import 'package:flutter_svg/flutter_svg.dart';

Widget buildIcon() {
  return SvgPicture.asset(
    'assets/icons/custom_icon.svg',
    width: 24,
    height: 24,
    colorFilter: const ColorFilter.mode(
      Colors.black,
      BlendMode.srcIn,
    ),
  );
}
```

## ProGuard/R8 for Android

R8 (the successor to ProGuard) is enabled by default in release builds. It
performs code shrinking, obfuscation, and optimization on the Java/Kotlin
side.

### Configuration

```groovy
// android/app/build.gradle
android {
    buildTypes {
        release {
            minifyEnabled true   // Enable R8 code shrinking
            shrinkResources true // Remove unused Android resources
            proguardFiles getDefaultProguardFile('proguard-android-optimize.txt'),
                          'proguard-rules.pro'
        }
    }
}
```

### Flutter-Specific ProGuard Rules

```
# android/app/proguard-rules.pro

# Flutter wrapper
-keep class io.flutter.app.** { *; }
-keep class io.flutter.plugin.** { *; }
-keep class io.flutter.util.** { *; }
-keep class io.flutter.view.** { *; }
-keep class io.flutter.** { *; }
-keep class io.flutter.plugins.** { *; }

# Keep Dart entry points
-keep class io.flutter.embedding.** { *; }

# Firebase (if used)
-keep class com.google.firebase.** { *; }

# Add rules for any plugins that use reflection
# Check each plugin's README for ProGuard requirements
```

### Verifying R8 Impact

```bash
# Build with and without R8, compare sizes
flutter build apk --release --analyze-size

# Check for R8 issues (missing keep rules)
flutter build apk --release 2>&1 | grep -i "warning\|error"
```

## Bitcode for iOS

> **Note:** As of Xcode 14, Apple has deprecated Bitcode. Flutter no longer
> supports Bitcode submission. Ensure your `Podfile` and build settings
> disable Bitcode:

```ruby
# ios/Podfile
post_install do |installer|
  installer.pods_project.targets.each do |target|
    target.build_configurations.each do |config|
      config.build_settings['ENABLE_BITCODE'] = 'NO'
    end
  end
end
```

For iOS size optimization, focus on:

- **App Thinning:** Ensure your app supports app thinning so the App Store
  delivers only the assets needed for each device.
- **Asset catalogs:** Use Xcode asset catalogs for images so the App Store
  can strip unused variants.
- **dSYM stripping:** Debug symbols are stripped automatically in release
  builds.

## Startup Time Optimization

### Measuring Startup Time

```bash
# Trace startup on Android
flutter run --profile --trace-startup

# This generates a timeline JSON at:
# build/start_up_info.json
```

Key metrics:

| Metric | Definition | Target |
|---|---|---|
| `engineEnterTimestampMicros` | Engine initialization start | Platform-dependent |
| `timeToFrameworkInitMicros` | Time from engine start to framework init | < 200 ms |
| `timeToFirstFrameMicros` | Time from engine start to first frame rendered | < 500 ms |

### Reducing main() Work

```dart
// BAD: synchronous initialization blocks first frame
void main() {
  final db = openDatabase();              // 50 ms
  final prefs = loadPreferences();         // 30 ms
  final analytics = initAnalytics();       // 40 ms
  configureDependencyInjection(db, prefs); // 20 ms

  runApp(MyApp(analytics: analytics));
  // Time to first frame: 140 ms + framework init
}

// GOOD: minimal synchronous work, defer everything possible
void main() {
  // Ensure Flutter binding is initialized
  WidgetsFlutterBinding.ensureInitialized();

  // Run the app immediately with a shell
  runApp(const AppShell());
}

/// Lightweight shell that shows a splash screen while initializing.
class AppShell extends StatefulWidget {
  const AppShell({super.key});

  @override
  State<AppShell> createState() => _AppShellState();
}

class _AppShellState extends State<AppShell> {
  late final Future<AppDependencies> _initFuture;

  @override
  void initState() {
    super.initState();
    _initFuture = _initializeApp();
  }

  Future<AppDependencies> _initializeApp() async {
    // All initialization is async and non-blocking
    final results = await (
      openDatabaseAsync(),
      loadPreferencesAsync(),
      initAnalyticsAsync(),
    ).wait;

    return AppDependencies(
      db: results.$1,
      prefs: results.$2,
      analytics: results.$3,
    );
  }

  @override
  Widget build(BuildContext context) {
    return FutureBuilder<AppDependencies>(
      future: _initFuture,
      builder: (context, snapshot) {
        if (snapshot.connectionState != ConnectionState.done) {
          return const MaterialApp(
            home: SplashScreen(),
          );
        }

        if (snapshot.hasError) {
          return MaterialApp(
            home: ErrorScreen(error: snapshot.error!),
          );
        }

        return MyApp(dependencies: snapshot.data!);
      },
    );
  }
}

class AppDependencies {
  const AppDependencies({
    required this.db,
    required this.prefs,
    required this.analytics,
  });

  final Database db;
  final SharedPreferences prefs;
  final AnalyticsService analytics;
}
```

### Parallel Initialization with Records (Dart 3+)

```dart
/// Initialize multiple services in parallel using Dart 3 records.
Future<({Database db, SharedPreferences prefs, AnalyticsService analytics})>
    initServices() async {
  final (db, prefs, analytics) = await (
    openDatabaseAsync(),
    SharedPreferences.getInstance(),
    AnalyticsService.initialize(),
  ).wait;

  return (db: db, prefs: prefs, analytics: analytics);
}
```

## Lazy Initialization Patterns

### late final for Expensive Singletons

```dart
class AppConfig {
  AppConfig._();

  static final instance = AppConfig._();

  // Initialized only on first access
  late final Map<String, dynamic> remoteConfig = _loadRemoteConfig();

  Map<String, dynamic> _loadRemoteConfig() {
    // Expensive synchronous operation
    // Only runs the first time remoteConfig is accessed
    return {};
  }
}
```

### Lazy with Async Initialization

```dart
class LazyService<T> {
  LazyService(this._factory);

  final Future<T> Function() _factory;
  T? _instance;
  Future<T>? _pending;

  Future<T> get instance {
    if (_instance case final cached?) {
      return Future.value(cached);
    }
    return _pending ??= _factory().then((value) {
      _instance = value;
      _pending = null;
      return value;
    });
  }

  bool get isInitialized => _instance != null;

  void dispose() {
    final inst = _instance;
    if (inst is ChangeNotifier) {
      inst.dispose();
    }
    _instance = null;
  }
}

// Usage:
final heavyService = LazyService(() async {
  final data = await loadExpensiveData();
  return HeavyService(data);
});

// First call initializes; subsequent calls return cached instance
final service = await heavyService.instance;
```

### Feature Flag Gated Initialization

```dart
class FeatureInitializer {
  FeatureInitializer({required this.featureFlags});

  final FeatureFlags featureFlags;

  final Map<String, LazyService<dynamic>> _services = {};

  /// Register a service that is only initialized if its feature flag is on.
  void register<T>(
    String featureKey,
    Future<T> Function() factory,
  ) {
    _services[featureKey] = LazyService(factory);
  }

  /// Get a service. Returns null if the feature is disabled.
  Future<T?> get<T>(String featureKey) async {
    if (!featureFlags.isEnabled(featureKey)) {
      return null;
    }
    return await _services[featureKey]?.instance as T?;
  }
}

abstract class FeatureFlags {
  bool isEnabled(String key);
}
```

### Startup Time Optimization Checklist

| Optimization | Impact | Effort |
|---|---|---|
| Move initialization to async | High -- unblocks first frame | Low |
| Parallelize independent init steps | Medium -- reduces total wait | Low |
| Defer non-critical services | Medium -- reduces startup work | Medium |
| Use a native splash screen | High -- perceived performance | Low |
| Precompile shaders (Impeller) | High -- eliminates first-frame jank | None (default on iOS) |
| Minimize plugin count | Medium -- each plugin adds init overhead | Medium |
| Profile with `--trace-startup` | Required -- measure before optimizing | Low |
