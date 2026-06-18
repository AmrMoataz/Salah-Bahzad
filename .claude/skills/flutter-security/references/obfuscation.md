# Obfuscation & Code Protection

## Dart Obfuscation

Flutter's built-in obfuscation renames classes, methods, and fields to
meaningless identifiers, making reverse engineering significantly harder.

### Build Command

```bash
flutter build apk \
  --obfuscate \
  --split-debug-info=build/symbols/android

flutter build ipa \
  --obfuscate \
  --split-debug-info=build/symbols/ios
```

- `--obfuscate` -- renames all Dart symbols.
- `--split-debug-info=<dir>` -- **required** alongside `--obfuscate`. Emits
  debug symbols to a separate directory so crash reports can be symbolicated
  without shipping symbols in the binary.

### Storing Symbol Maps

Symbol maps are essential for debugging production crashes. Store them as
versioned build artifacts in your CI/CD pipeline.

```yaml
# Example: GitHub Actions step to archive symbols
- name: Archive debug symbols
  uses: actions/upload-artifact@v4
  with:
    name: debug-symbols-${{ github.sha }}
    path: build/symbols/
    retention-days: 365
```

### Symbolicating Stack Traces

Use the `flutter symbolize` command to restore human-readable stack traces
from obfuscated crash reports.

```bash
flutter symbolize \
  --input=crash_report.txt \
  --debug-info=build/symbols/android/ \
  --output=symbolicated_report.txt
```

---

## ProGuard and R8 Rules for Android

R8 (the default Android code shrinker since AGP 3.4) performs tree-shaking,
optimization, and obfuscation of Java/Kotlin code. Flutter apps need specific
keep rules to prevent R8 from stripping symbols that the engine or plugins
reference via reflection.

### Enabling R8 Shrinking

In `android/app/build.gradle`:

```groovy
android {
    buildTypes {
        release {
            minifyEnabled true
            shrinkResources true
            proguardFiles getDefaultProguardFile('proguard-android-optimize.txt'),
                          'proguard-rules.pro'
        }
    }
}
```

### ProGuard Rules File

```proguard
# android/app/proguard-rules.pro

# ── Flutter engine ──────────────────────────────────────────────────────
-keep class io.flutter.** { *; }
-keep class io.flutter.embedding.** { *; }
-dontwarn io.flutter.embedding.**

# ── Dart VM snapshots ──────────────────────────────────────────────────
-keep class io.flutter.app.FlutterApplication { *; }

# ── Plugin: flutter_secure_storage ─────────────────────────────────────
-keep class com.it_nomads.fluttersecurestorage.** { *; }

# ── Plugin: local_auth ─────────────────────────────────────────────────
-keep class io.flutter.plugins.localauth.** { *; }

# ── AndroidX Security (EncryptedSharedPreferences) ─────────────────────
-keep class androidx.security.crypto.** { *; }
-keep class com.google.crypto.tink.** { *; }
-dontwarn com.google.crypto.tink.**

# ── Google Play Services (if used) ─────────────────────────────────────
-keep class com.google.android.gms.** { *; }
-dontwarn com.google.android.gms.**

# ── Gson / JSON serialization (if used by plugins) ────────────────────
-keepattributes Signature
-keepattributes *Annotation*
-keep class com.google.gson.** { *; }

# ── General safety ─────────────────────────────────────────────────────
-keepattributes SourceFile,LineNumberTable   # Better crash reports
-renamesourcefileattribute SourceFile
```

### Verifying R8 Output

After a release build, inspect the mapping file to confirm symbols were renamed:

```bash
# The mapping file is generated alongside the APK
cat android/app/build/outputs/mapping/release/mapping.txt | head -50
```

Archive `mapping.txt` with every release for crash report symbolication.

---

## Symbol Mapping for Crash Reports

Production crash reporting (Firebase Crashlytics, Sentry, Bugsnag) requires both
the **Dart symbol map** and the **R8 mapping file** to produce readable stack
traces.

### Firebase Crashlytics Integration

```yaml
# pubspec.yaml
dependencies:
  firebase_crashlytics: ^4.3.2
  firebase_core: ^3.12.1
```

Upload symbols automatically in CI:

```bash
# Upload Dart debug symbols to Crashlytics
firebase crashlytics:symbols:upload \
  --app=1:123456789:android:abcdef \
  build/symbols/android/

# R8 mapping is uploaded automatically by the Gradle plugin.
```

### Sentry Integration

```bash
# Upload Dart debug info to Sentry
sentry-cli debug-files upload \
  --org my-org \
  --project my-flutter-app \
  build/symbols/android/

# Upload ProGuard/R8 mapping
sentry-cli upload-proguard \
  --org my-org \
  --project my-flutter-app \
  android/app/build/outputs/mapping/release/mapping.txt
```

---

## Jailbreak and Root Detection

Jailbroken iOS devices and rooted Android devices bypass platform security
controls. Detecting these environments lets you warn users or restrict
functionality.

### flutter_jailbreak_detection

```yaml
# pubspec.yaml
dependencies:
  flutter_jailbreak_detection: ^1.10.0
```

```dart
// lib/core/security/device_integrity_service.dart

import 'package:flutter_jailbreak_detection/flutter_jailbreak_detection.dart';

/// Checks whether the device has been jailbroken (iOS) or rooted (Android).
class DeviceIntegrityService {
  /// Returns `true` when the device appears to be compromised.
  Future<bool> get isCompromised async {
    try {
      return await FlutterJailbreakDetection.jailbroken;
    } on Exception {
      // If the check itself fails, treat the device as suspect.
      return true;
    }
  }

  /// Returns `true` when a developer mode flag is active.
  /// On Android this checks for USB debugging; on iOS it is always `false`.
  Future<bool> get isDeveloperMode async {
    try {
      return await FlutterJailbreakDetection.developerMode;
    } on Exception {
      return false;
    }
  }
}
```

### Enforcement Strategies

Choose a strategy that matches your threat model:

```dart
// lib/core/security/integrity_gate.dart

import 'package:flutter/material.dart';

import 'device_integrity_service.dart';

enum IntegrityPolicy {
  /// Allow usage but log the event.
  warnAndLog,

  /// Show a non-dismissible warning but let the user continue.
  warnAndContinue,

  /// Block access entirely.
  block,
}

/// A widget that gates its [child] behind a device integrity check.
class IntegrityGate extends StatefulWidget {
  const IntegrityGate({
    required this.child,
    this.policy = IntegrityPolicy.warnAndContinue,
    super.key,
  });

  final Widget child;
  final IntegrityPolicy policy;

  @override
  State<IntegrityGate> createState() => _IntegrityGateState();
}

class _IntegrityGateState extends State<IntegrityGate> {
  final _integrity = DeviceIntegrityService();
  bool _checked = false;
  bool _compromised = false;

  @override
  void initState() {
    super.initState();
    _checkIntegrity();
  }

  Future<void> _checkIntegrity() async {
    final result = await _integrity.isCompromised;
    if (mounted) {
      setState(() {
        _checked = true;
        _compromised = result;
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    if (!_checked) {
      return const Scaffold(
        body: Center(child: CircularProgressIndicator()),
      );
    }

    if (_compromised) {
      switch (widget.policy) {
        case IntegrityPolicy.block:
          return Scaffold(
            body: Center(
              child: Padding(
                padding: const EdgeInsets.all(24),
                child: Text(
                  'This app cannot run on a rooted or jailbroken device.',
                  style: Theme.of(context).textTheme.titleMedium,
                  textAlign: TextAlign.center,
                ),
              ),
            ),
          );

        case IntegrityPolicy.warnAndContinue:
          WidgetsBinding.instance.addPostFrameCallback((_) {
            if (mounted) {
              ScaffoldMessenger.of(context).showSnackBar(
                const SnackBar(
                  content: Text(
                    'Warning: This device may be compromised. '
                    'Some features may be restricted.',
                  ),
                  duration: Duration(seconds: 5),
                ),
              );
            }
          });
          return widget.child;

        case IntegrityPolicy.warnAndLog:
          // In production, send a telemetry event here.
          debugPrint('[SECURITY] Device integrity check failed.');
          return widget.child;
      }
    }

    return widget.child;
  }
}
```

Usage in `main.dart`:

```dart
// lib/main.dart

import 'package:flutter/material.dart';

import 'core/security/integrity_gate.dart';

void main() {
  runApp(
    const IntegrityGate(
      policy: IntegrityPolicy.warnAndContinue,
      child: MyApp(),
    ),
  );
}

class MyApp extends StatelessWidget {
  const MyApp({super.key});

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      title: 'Secure App',
      home: const Scaffold(body: Center(child: Text('Home'))),
    );
  }
}
```

---

## Tamper Detection

Tamper detection verifies that the application binary has not been modified after
it was signed.

### Android -- Signature Verification

```dart
// lib/core/security/tamper_detection_android.dart

import 'dart:convert';

import 'package:crypto/crypto.dart';
import 'package:flutter/services.dart';

/// Verifies the APK signing certificate hash at runtime.
///
/// Compare the runtime hash against the known-good hash from your CI build.
class AndroidTamperDetector {
  static const _channel = MethodChannel('com.example.app/tamper');

  /// Returns the SHA-256 fingerprint of the first APK signing certificate.
  ///
  /// Compare this value against the fingerprint you recorded at build time.
  static Future<String> getSigningCertHash() async {
    final String hash = await _channel.invokeMethod('getSigningCertHash');
    return hash;
  }

  /// Returns `true` if the signing certificate matches the expected hash.
  static Future<bool> isGenuine(String expectedHash) async {
    final currentHash = await getSigningCertHash();
    // Constant-time comparison to prevent timing attacks.
    return _constantTimeEquals(currentHash, expectedHash);
  }

  static bool _constantTimeEquals(String a, String b) {
    if (a.length != b.length) return false;
    var result = 0;
    for (var i = 0; i < a.length; i++) {
      result |= a.codeUnitAt(i) ^ b.codeUnitAt(i);
    }
    return result == 0;
  }
}
```

The native side (Kotlin) for the method channel:

```kotlin
// android/app/src/main/kotlin/.../TamperPlugin.kt

package com.example.app

import android.content.pm.PackageManager
import android.os.Build
import io.flutter.embedding.engine.FlutterEngine
import io.flutter.plugin.common.MethodChannel
import java.security.MessageDigest

fun registerTamperChannel(flutterEngine: FlutterEngine, packageManager: PackageManager, packageName: String) {
    MethodChannel(flutterEngine.dartExecutor.binaryMessenger, "com.example.app/tamper")
        .setMethodCallHandler { call, result ->
            when (call.method) {
                "getSigningCertHash" -> {
                    try {
                        val signingInfo = if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.P) {
                            packageManager.getPackageInfo(packageName, PackageManager.GET_SIGNING_CERTIFICATES)
                                .signingInfo.apkContentsSigners
                        } else {
                            @Suppress("DEPRECATION")
                            packageManager.getPackageInfo(packageName, PackageManager.GET_SIGNATURES)
                                .signatures
                        }

                        val cert = signingInfo.first()
                        val md = MessageDigest.getInstance("SHA-256")
                        val hash = md.digest(cert.toByteArray())
                        val hex = hash.joinToString("") { "%02x".format(it) }
                        result.success(hex)
                    } catch (e: Exception) {
                        result.error("TAMPER_ERROR", e.message, null)
                    }
                }
                else -> result.notImplemented()
            }
        }
}
```

---

## Reverse Engineering Prevention Strategies

No technique makes reverse engineering impossible, but layering defences raises
the cost significantly.

| Layer | Technique | Effect |
|---|---|---|
| 1 | `--obfuscate` | Strips meaningful symbol names from Dart code |
| 2 | ProGuard / R8 | Shrinks, optimizes, and obfuscates Java/Kotlin |
| 3 | `envied` with `obfuscate: true` | XOR-encrypts string constants so they are not trivially grep-able |
| 4 | Certificate pinning | Prevents traffic interception for dynamic analysis |
| 5 | Root/jailbreak detection | Detects environments where Frida/Xposed are commonly used |
| 6 | Tamper detection | Verifies the binary has not been repackaged |
| 7 | Code attestation (Play Integrity / App Attest) | Platform-level verification that the binary and device are genuine |

### Play Integrity API (Android)

```dart
// lib/core/security/play_integrity_service.dart

import 'package:flutter/services.dart';

/// Requests a Play Integrity verdict token from Google Play Services.
///
/// The token must be verified **server-side** by calling the Google Play
/// Integrity API. Never trust the result on-device alone.
class PlayIntegrityService {
  static const _channel = MethodChannel('com.example.app/play_integrity');

  /// Requests an integrity token for the given [nonce].
  ///
  /// The [nonce] should be a unique, server-generated, base64-encoded value
  /// for each request to prevent replay attacks.
  ///
  /// Returns the integrity token string to be sent to your backend for
  /// verification.
  static Future<String?> requestIntegrityToken(String nonce) async {
    try {
      final String? token = await _channel.invokeMethod(
        'requestIntegrityToken',
        {'nonce': nonce},
      );
      return token;
    } on PlatformException {
      return null;
    }
  }
}
```

---

## Secure Logging

Logs are a common source of accidental data leakage. In production builds, no
log statement should contain tokens, passwords, PII, or cryptographic material.

### Logging Service with Sanitization

```dart
// lib/core/logging/secure_logger.dart

import 'package:flutter/foundation.dart';

/// A logger that automatically redacts sensitive patterns in release mode
/// and suppresses verbose output.
class SecureLogger {
  SecureLogger({this.tag = 'APP'});

  final String tag;

  /// Patterns that are always redacted, even in debug mode.
  static final _sensitivePatterns = [
    RegExp(r'Bearer\s+[A-Za-z0-9\-._~+/]+=*', caseSensitive: false),
    RegExp(r'"(password|secret|token|api_key|apiKey|authorization)"\s*:\s*"[^"]*"',
        caseSensitive: false),
    RegExp(r'eyJ[A-Za-z0-9_-]+\.eyJ[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+'), // JWT
  ];

  /// Logs at the info level. Messages are sanitized.
  void info(String message) {
    if (kReleaseMode) return; // No info logs in release.
    debugPrint('[$tag] INFO: ${_sanitize(message)}');
  }

  /// Logs at the warning level. Messages are sanitized.
  void warning(String message) {
    debugPrint('[$tag] WARN: ${_sanitize(message)}');
  }

  /// Logs at the error level. Always emitted but sanitized.
  void error(String message, [Object? error, StackTrace? stackTrace]) {
    debugPrint('[$tag] ERROR: ${_sanitize(message)}');
    if (error != null) {
      debugPrint('[$tag] ERROR detail: ${_sanitize(error.toString())}');
    }
    if (stackTrace != null && kDebugMode) {
      debugPrint('[$tag] STACK: $stackTrace');
    }
  }

  String _sanitize(String input) {
    var output = input;
    for (final pattern in _sensitivePatterns) {
      output = output.replaceAll(pattern, '[REDACTED]');
    }
    return output;
  }
}
```

### Rules for Developers

1. **Never** pass raw request/response bodies to `print` or `debugPrint`.
2. Use the `SecureLogger` for all application-level logging.
3. Disable Dio's `LogInterceptor` in release builds:

```dart
if (kDebugMode) {
  dio.interceptors.add(LogInterceptor(
    requestBody: true,
    responseBody: true,
    logPrint: (msg) => SecureLogger(tag: 'HTTP').info(msg.toString()),
  ));
}
```

4. Never log biometric results, PIN codes, or OTP values.
5. Audit log statements in code review -- treat any log containing user data as
   a security bug.

---

## Runtime Application Self-Protection (RASP) Concepts

RASP embeds security checks directly into the running application so it can
detect and respond to attacks in real time, without relying solely on perimeter
defences.

### Core RASP Checks for Flutter

```dart
// lib/core/security/rasp_service.dart

import 'device_integrity_service.dart';

/// Result of a comprehensive RASP assessment.
class RaspReport {
  const RaspReport({
    required this.isRootedOrJailbroken,
    required this.isDeveloperModeEnabled,
    required this.isRunningInEmulator,
    required this.isDebuggerAttached,
  });

  final bool isRootedOrJailbroken;
  final bool isDeveloperModeEnabled;
  final bool isRunningInEmulator;
  final bool isDebuggerAttached;

  /// Returns `true` when any check indicates a potentially hostile
  /// environment.
  bool get isEnvironmentHostile =>
      isRootedOrJailbroken ||
      isDebuggerAttached ||
      isRunningInEmulator;

  @override
  String toString() =>
      'RaspReport(rooted=$isRootedOrJailbroken, devMode=$isDeveloperModeEnabled, '
      'emulator=$isRunningInEmulator, debugger=$isDebuggerAttached)';
}

/// Orchestrates multiple runtime integrity checks into a single report.
class RaspService {
  RaspService({DeviceIntegrityService? integrityService})
      : _integrity = integrityService ?? DeviceIntegrityService();

  final DeviceIntegrityService _integrity;

  /// Runs all RASP checks and returns a consolidated report.
  Future<RaspReport> assess() async {
    final results = await Future.wait([
      _integrity.isCompromised,
      _integrity.isDeveloperMode,
      _checkEmulator(),
      _checkDebugger(),
    ]);

    return RaspReport(
      isRootedOrJailbroken: results[0],
      isDeveloperModeEnabled: results[1],
      isRunningInEmulator: results[2],
      isDebuggerAttached: results[3],
    );
  }

  /// Heuristic emulator detection.
  ///
  /// For production use, supplement this with a native platform channel that
  /// checks Build.FINGERPRINT, Build.MODEL, and similar Android properties,
  /// or the Darwin sysctl hw.machine value on iOS simulators.
  Future<bool> _checkEmulator() async {
    // A robust implementation would use a platform channel.
    // This is a placeholder for the pattern -- replace with native checks.
    return false;
  }

  /// Checks whether a debugger is currently attached.
  ///
  /// In Dart, `assert` statements only execute in debug mode, but there is
  /// no direct Dart API to detect an attached native debugger. Use a
  /// platform channel that calls `Debug.isDebuggerConnected()` on Android
  /// or `sysctl` with `P_TRACED` on iOS.
  Future<bool> _checkDebugger() async {
    var isDebug = false;
    assert(() {
      isDebug = true;
      return true;
    }());
    return isDebug;
  }
}
```

### RASP Response Actions

When a hostile environment is detected, choose from a graduated response:

```dart
// lib/core/security/rasp_enforcer.dart

import 'package:flutter/foundation.dart';

import '../logging/secure_logger.dart';
import 'rasp_service.dart';
import 'secure_storage_service.dart';

/// Takes action based on a [RaspReport].
class RaspEnforcer {
  RaspEnforcer({
    required SecureStorageService storage,
    SecureLogger? logger,
  })  : _storage = storage,
        _logger = logger ?? SecureLogger(tag: 'RASP');

  final SecureStorageService _storage;
  final SecureLogger _logger;

  /// Evaluates the report and takes appropriate action.
  ///
  /// Returns `true` when the app should continue normally, or `false`
  /// when the app should block the user from proceeding.
  Future<bool> enforce(RaspReport report) async {
    if (!report.isEnvironmentHostile) {
      return true; // Environment is clean.
    }

    _logger.warning('RASP check failed: $report');

    // ── Debugger attached ────────────────────────────────────────────
    if (report.isDebuggerAttached && kReleaseMode) {
      _logger.error('Debugger detected in release build. Wiping secrets.');
      await _storage.clearAll();
      return false;
    }

    // ── Emulator in release ──────────────────────────────────────────
    if (report.isRunningInEmulator && kReleaseMode) {
      _logger.warning('Emulator detected in release build.');
      // Depending on your policy, you may allow or block.
      return false;
    }

    // ── Rooted / Jailbroken ──────────────────────────────────────────
    if (report.isRootedOrJailbroken) {
      _logger.warning('Device is rooted/jailbroken.');
      // Allow with reduced functionality, or block entirely.
      return true; // Policy: warn but allow.
    }

    return true;
  }
}
```

### Running RASP at App Startup

```dart
// lib/main.dart (RASP integration)

import 'package:flutter/material.dart';

import 'core/security/rasp_enforcer.dart';
import 'core/security/rasp_service.dart';
import 'core/security/secure_storage_service.dart';

Future<void> main() async {
  WidgetsFlutterBinding.ensureInitialized();

  final storage = SecureStorageService();
  final raspService = RaspService();
  final enforcer = RaspEnforcer(storage: storage);

  final report = await raspService.assess();
  final shouldContinue = await enforcer.enforce(report);

  if (!shouldContinue) {
    runApp(const _BlockedApp());
    return;
  }

  runApp(const MyApp());
}

class _BlockedApp extends StatelessWidget {
  const _BlockedApp();

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      home: Scaffold(
        body: Center(
          child: Padding(
            padding: const EdgeInsets.all(32),
            child: Text(
              'This app cannot run in the current environment.\n'
              'Please use an unmodified device.',
              textAlign: TextAlign.center,
              style: const TextStyle(fontSize: 18),
            ),
          ),
        ),
      ),
    );
  }
}

class MyApp extends StatelessWidget {
  const MyApp({super.key});

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      title: 'Secure App',
      home: const Scaffold(body: Center(child: Text('Home'))),
    );
  }
}
```
