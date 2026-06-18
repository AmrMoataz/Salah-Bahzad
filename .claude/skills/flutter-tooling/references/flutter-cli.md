# Flutter CLI Comprehensive Guide

## flutter create -- Project Creation

### Basic Usage

```bash
# Create a standard application
flutter create my_app

# Specify organization (reverse domain)
flutter create --org com.mycompany my_app

# Choose specific platforms
flutter create --platforms ios,android,web,macos,linux,windows my_app

# Combine options
flutter create --template app --org com.mycompany --platforms ios,android,web my_app
```

### Templates

```bash
# Full application (default)
flutter create --template app my_app

# Package (for publishing to pub.dev)
flutter create --template package my_package

# Plugin (platform-specific native code)
flutter create --template plugin my_plugin

# Plugin with FFI (Foreign Function Interface)
flutter create --template plugin_ffi my_ffi_plugin

# Skeleton app (opinionated, production-ready starter)
flutter create --template skeleton my_app
```

### Additional Create Options

```bash
# Specify a description
flutter create --description "A sample Flutter app" my_app

# Use a specific project name (differs from directory name)
flutter create --project-name my_project ./my-directory

# Create with empty template (minimal boilerplate)
flutter create --empty my_app

# Specify the sample code to use (from API docs)
flutter create --sample=widgets.SingleChildScrollView.1 my_app
```

---

## flutter run -- Running Applications

### Basic Run Commands

```bash
# Run in debug mode (default)
flutter run

# Run on a specific device
flutter run -d <device_id>

# List available devices
flutter devices

# Run on Chrome (web)
flutter run -d chrome

# Run on all attached devices
flutter run -d all
```

### Build Modes

```bash
# Debug mode (default) -- hot reload, assertions enabled, dev tools
flutter run --debug

# Profile mode -- performance profiling, no debugger
flutter run --profile

# Release mode -- optimized, no debugging
flutter run --release
```

### Dart Defines (Compile-time Variables)

```bash
# Pass a single compile-time variable
flutter run --dart-define=API_BASE_URL=https://api.example.com

# Pass multiple variables
flutter run \
  --dart-define=API_BASE_URL=https://api.example.com \
  --dart-define=ENVIRONMENT=staging \
  --dart-define=ENABLE_LOGGING=true

# Use a defines file (one KEY=VALUE per line)
flutter run --dart-define-from-file=config/dev.env
```

Reading dart defines in code:

```dart
const apiBaseUrl = String.fromEnvironment('API_BASE_URL', defaultValue: 'https://localhost');
const environment = String.fromEnvironment('ENVIRONMENT', defaultValue: 'development');
const enableLogging = bool.fromEnvironment('ENABLE_LOGGING', defaultValue: false);
```

### Additional Run Options

```bash
# Specify a target file (other than lib/main.dart)
flutter run -t lib/main_staging.dart

# Specify a flavor (Android product flavors / iOS schemes)
flutter run --flavor staging

# Combine flavor and target
flutter run --flavor production -t lib/main_production.dart

# Enable hot reload on save (VS Code does this automatically)
# Press 'r' in terminal for hot reload, 'R' for hot restart

# Verbose logging
flutter run --verbose

# Specify a specific web renderer
flutter run -d chrome --web-renderer canvaskit
flutter run -d chrome --web-renderer html
```

---

## flutter build -- Building Applications

### Android Builds

```bash
# Build APK (fat APK with all ABIs)
flutter build apk

# Build APK in release mode
flutter build apk --release

# Build split APKs (per ABI -- smaller downloads)
flutter build apk --split-per-abi

# Build Android App Bundle (recommended for Play Store)
flutter build appbundle
flutter build appbundle --release

# Build with a flavor
flutter build appbundle --flavor production -t lib/main_production.dart

# Build with obfuscation and split debug info
flutter build appbundle --obfuscate --split-debug-info=build/debug-info

# Build with a specific target platform
flutter build apk --target-platform android-arm,android-arm64,android-x64

# Specify build number and name
flutter build appbundle --build-number=42 --build-name=1.2.3
```

### iOS Builds

```bash
# Build iOS (requires macOS with Xcode)
flutter build ios

# Build iOS in release mode
flutter build ios --release

# Build without codesigning (for CI)
flutter build ios --no-codesign

# Build IPA for distribution
flutter build ipa

# Build IPA with export options
flutter build ipa --export-options-plist=ios/ExportOptions.plist

# Build with obfuscation
flutter build ios --obfuscate --split-debug-info=build/debug-info
```

### Web Builds

```bash
# Build for web
flutter build web

# Build with a specific renderer
flutter build web --web-renderer canvaskit
flutter build web --web-renderer html

# Build with a specific base href (for subdirectory deployments)
flutter build web --base-href /my-app/

# Build with tree shaking of Material Design icons
flutter build web --tree-shake-icons
```

### Desktop Builds

```bash
# macOS (requires macOS)
flutter build macos

# Linux
flutter build linux

# Windows
flutter build windows
```

### Common Build Flags

```bash
# Applicable to most build commands:
--release              # Release mode (default for build)
--debug                # Debug mode
--profile              # Profile mode
--obfuscate            # Obfuscate Dart code
--split-debug-info=DIR # Output debug symbols to directory
--build-number=N       # Set build number (versionCode / CFBundleVersion)
--build-name=X.Y.Z    # Set version name (versionName / CFBundleShortVersionString)
--dart-define=KEY=VAL  # Compile-time variable
--dart-define-from-file=PATH  # Compile-time variables from file
--flavor NAME          # Build a specific flavor/scheme
-t, --target=PATH     # Specify entry point file
```

---

## flutter test -- Testing

### Running Tests

```bash
# Run all tests
flutter test

# Run a specific test file
flutter test test/widget_test.dart

# Run tests in a specific directory
flutter test test/unit/

# Run a specific test by name
flutter test --name "should display login button"

# Run tests matching a pattern
flutter test --plain-name "login"

# Run tests with tags
flutter test --tags integration
flutter test --exclude-tags slow
```

### Coverage

```bash
# Run tests with coverage collection
flutter test --coverage

# Coverage output is at coverage/lcov.info
# Generate HTML report (requires lcov)
genhtml coverage/lcov.info -o coverage/html
open coverage/html/index.html

# Run coverage for specific files
flutter test --coverage test/unit/
```

### Golden Tests

```bash
# Update golden files (reference screenshots)
flutter test --update-goldens

# Update goldens for a specific test file
flutter test --update-goldens test/golden/my_widget_test.dart
```

### Additional Test Options

```bash
# Run tests with concurrency control
flutter test --concurrency=1

# Run tests with verbose output
flutter test --reporter expanded

# Run tests with a specific reporter
flutter test --reporter compact
flutter test --reporter json

# Set a timeout for all tests
flutter test --timeout 60s

# Run integration tests
flutter test integration_test/

# Run integration tests on a device
flutter drive --driver=test_driver/integration_test.dart \
  --target=integration_test/app_test.dart
```

---

## flutter doctor -- Environment Diagnostics

```bash
# Check environment setup
flutter doctor

# Verbose output with full details
flutter doctor -v

# Example output:
# [ok] Flutter (Channel stable, 3.24.0)
# [ok] Android toolchain - develop for Android devices
# [ok] Xcode - develop for iOS and macOS
# [ok] Chrome - develop for the web
# [ok] Android Studio
# [ok] VS Code
# [ok] Connected device (3 available)
# [ok] Network resources
```

---

## flutter clean -- Cleaning Build Artifacts

```bash
# Remove build/ and .dart_tool/ directories
flutter clean

# Common pattern: clean and re-fetch dependencies
flutter clean && flutter pub get

# Full reset including code generation
flutter clean && flutter pub get && dart run build_runner build --delete-conflicting-outputs
```

---

## flutter pub -- Package Management

### Dependency Management

```bash
# Fetch dependencies listed in pubspec.yaml
flutter pub get

# Upgrade dependencies to latest allowed versions
flutter pub upgrade

# Upgrade a specific package
flutter pub upgrade freezed

# Upgrade to latest major versions (breaking changes)
flutter pub upgrade --major-versions

# Add a dependency
flutter pub add http
flutter pub add dio

# Add a dev dependency
flutter pub add --dev build_runner
flutter pub add --dev freezed

# Remove a dependency
flutter pub remove http

# Check for outdated packages
flutter pub outdated

# Check for dependency resolution issues
flutter pub deps

# Show dependency tree
flutter pub deps --style=tree
```

### Publishing

```bash
# Dry run before publishing
flutter pub publish --dry-run

# Publish to pub.dev
flutter pub publish

# Publish with force (skip confirmation)
flutter pub publish --force
```

### Global Packages

```bash
# Activate a global package
flutter pub global activate devtools
flutter pub global activate flutterfire_cli

# Run a globally activated package
flutter pub global run devtools

# Deactivate a global package
flutter pub global deactivate devtools

# List globally activated packages
flutter pub global list
```

---

## flutter analyze -- Static Analysis

```bash
# Run Dart analyzer on the project
flutter analyze

# Analyze with fatal warnings (exit code 1 on warnings)
flutter analyze --fatal-warnings

# Analyze with fatal infos (exit code 1 on info-level issues)
flutter analyze --fatal-infos

# Analyze a specific directory
flutter analyze lib/

# Watch for changes and re-analyze
flutter analyze --watch
```

---

## flutter format / dart format -- Code Formatting

```bash
# Format all Dart files in the project
dart format .

# Format a specific file
dart format lib/main.dart

# Format a specific directory
dart format lib/src/

# Check formatting without modifying (useful in CI)
dart format --set-exit-if-changed .

# Set line length (default is 80)
dart format --line-length 120 .

# Show which files would change
dart format --output show .
```

> Note: `flutter format` delegates to `dart format`. Prefer `dart format` directly.

---

## FVM -- Flutter Version Management

### Installation

```bash
# Install via Homebrew (macOS)
brew tap leoafarias/fvm
brew install fvm

# Install via pub global
dart pub global activate fvm

# Install via Chocolatey (Windows)
choco install fvm

# Verify installation
fvm --version
```

### Basic Usage

```bash
# List available Flutter releases
fvm releases

# Install a specific Flutter version
fvm install 3.24.0

# Install the latest stable version
fvm install stable

# Install the latest beta version
fvm install beta

# Use a specific version in the current project
fvm use 3.24.0

# Use stable channel
fvm use stable

# Use and pin a version globally
fvm global 3.24.0

# List installed Flutter versions
fvm list

# Remove an installed version
fvm remove 3.19.0

# Show the current active version for the project
fvm current
```

### Project Configuration

When you run `fvm use`, it creates a `.fvmrc` file in the project root:

```json
{
  "flutter": "3.24.0"
}
```

Add to `.gitignore`:

```gitignore
# FVM
.fvm/flutter_sdk
```

The `.fvmrc` file should be committed to version control so all team members use the same Flutter version.

### IDE Configuration

**VS Code** -- add to `.vscode/settings.json`:

```json
{
  "dart.flutterSdkPath": ".fvm/flutter_sdk"
}
```

**Android Studio**:

1. Go to Languages & Frameworks > Flutter
2. Set Flutter SDK path to: `<project_root>/.fvm/flutter_sdk`

### Using FVM with Commands

```bash
# Run any Flutter command through FVM's pinned version
fvm flutter run
fvm flutter build appbundle
fvm flutter test
fvm dart run build_runner build
```

---

## flutter gen-l10n -- Localization

### Setup

Add the `flutter_localizations` dependency to `pubspec.yaml`:

```yaml
dependencies:
  flutter:
    sdk: flutter
  flutter_localizations:
    sdk: flutter
  intl: any

flutter:
  generate: true
```

Create `l10n.yaml` in the project root:

```yaml
arb-dir: lib/l10n
template-arb-file: app_en.arb
output-localization-file: app_localizations.dart
output-dir: lib/l10n/generated
synthetic-package: false
nullable-getter: false
```

### ARB Files

Create `lib/l10n/app_en.arb`:

```json
{
  "@@locale": "en",
  "appTitle": "My App",
  "@appTitle": {
    "description": "The title of the application"
  },
  "greeting": "Hello, {name}!",
  "@greeting": {
    "description": "Greeting message with user name",
    "placeholders": {
      "name": {
        "type": "String",
        "example": "Alice"
      }
    }
  },
  "itemCount": "{count, plural, =0{No items} =1{1 item} other{{count} items}}",
  "@itemCount": {
    "description": "Label for item count",
    "placeholders": {
      "count": {
        "type": "int"
      }
    }
  }
}
```

Create `lib/l10n/app_es.arb`:

```json
{
  "@@locale": "es",
  "appTitle": "Mi Aplicacion",
  "greeting": "Hola, {name}!",
  "itemCount": "{count, plural, =0{Sin elementos} =1{1 elemento} other{{count} elementos}}"
}
```

### Generate Localization Files

```bash
flutter gen-l10n
```

### Usage in Code

```dart
import 'package:flutter/material.dart';
import 'package:my_app/l10n/generated/app_localizations.dart';

class MyApp extends StatelessWidget {
  const MyApp({super.key});

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      localizationsDelegates: AppLocalizations.localizationsDelegates,
      supportedLocales: AppLocalizations.supportedLocales,
      home: const HomePage(),
    );
  }
}

class HomePage extends StatelessWidget {
  const HomePage({super.key});

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context);
    return Scaffold(
      appBar: AppBar(title: Text(l10n.appTitle)),
      body: Column(
        children: [
          Text(l10n.greeting('Alice')),
          Text(l10n.itemCount(5)),
        ],
      ),
    );
  }
}
```

---

## Useful Compound Commands

```bash
# Full project reset
flutter clean && flutter pub get && dart run build_runner build --delete-conflicting-outputs

# CI pipeline: analyze, test, build
flutter analyze --fatal-warnings && flutter test --coverage && flutter build appbundle --release

# Run on specific device with environment config
flutter run --flavor staging -t lib/main_staging.dart --dart-define-from-file=config/staging.env

# Update all dependencies and check for issues
flutter pub upgrade && flutter pub outdated && flutter analyze
```
