# Secure Storage & Authentication

## flutter_secure_storage -- Tokens and Secrets

`flutter_secure_storage` delegates to the platform keystore: **Keychain** on iOS /
macOS and **EncryptedSharedPreferences** (AES-256) on Android.

### Setup

```yaml
# pubspec.yaml
dependencies:
  flutter_secure_storage: ^9.2.4
```

Android -- set the minimum SDK to 23 and enable EncryptedSharedPreferences:

```xml
<!-- android/app/src/main/AndroidManifest.xml -->
<manifest xmlns:android="http://schemas.android.com/apk/res/android">
    <application
        android:allowBackup="false"
        android:fullBackupContent="false">
        <!-- ... -->
    </application>
</manifest>
```

### Core Wrapper Service

Centralise all storage access behind a single service so you can swap
implementations in tests and enforce key naming conventions.

```dart
// lib/core/security/secure_storage_service.dart

import 'package:flutter_secure_storage/flutter_secure_storage.dart';

/// Thin wrapper around [FlutterSecureStorage] that enforces key prefixes,
/// provides typed accessors, and makes testing straightforward.
class SecureStorageService {
  SecureStorageService({FlutterSecureStorage? storage})
      : _storage = storage ??
            const FlutterSecureStorage(
              aOptions: AndroidOptions(encryptedSharedPreferences: true),
              iOptions: IOSOptions(
                accessibility: KeychainAccessibility.first_unlock_this_device,
              ),
            );

  final FlutterSecureStorage _storage;

  // ── Key constants ────────────────────────────────────────────────────
  static const _accessTokenKey = 'auth_access_token';
  static const _refreshTokenKey = 'auth_refresh_token';
  static const _pinCodeKey = 'user_pin_code';

  // ── Token helpers ────────────────────────────────────────────────────

  Future<void> saveAccessToken(String token) =>
      _storage.write(key: _accessTokenKey, value: token);

  Future<String?> readAccessToken() => _storage.read(key: _accessTokenKey);

  Future<void> saveRefreshToken(String token) =>
      _storage.write(key: _refreshTokenKey, value: token);

  Future<String?> readRefreshToken() => _storage.read(key: _refreshTokenKey);

  // ── PIN ──────────────────────────────────────────────────────────────

  Future<void> savePinCode(String pin) =>
      _storage.write(key: _pinCodeKey, value: pin);

  Future<String?> readPinCode() => _storage.read(key: _pinCodeKey);

  // ── Generic helpers ──────────────────────────────────────────────────

  Future<void> writeSecret(String key, String value) =>
      _storage.write(key: key, value: value);

  Future<String?> readSecret(String key) => _storage.read(key: key);

  Future<void> deleteSecret(String key) => _storage.delete(key: key);

  /// Remove **all** stored secrets. Call this on logout.
  Future<void> clearAll() => _storage.deleteAll();
}
```

### iOS Keychain Access Groups

When your app uses an App Group or shares credentials with an extension, set the
access group in `IOSOptions`:

```dart
const storage = FlutterSecureStorage(
  iOptions: IOSOptions(
    accessibility: KeychainAccessibility.first_unlock_this_device,
    accountName: 'com.example.myapp',
    groupId: 'group.com.example.shared', // App Group identifier
  ),
);
```

Add the matching entitlement in `ios/Runner/Runner.entitlements`:

```xml
<key>keychain-access-groups</key>
<array>
    <string>$(AppIdentifierPrefix)group.com.example.shared</string>
</array>
```

### Android EncryptedSharedPreferences

`flutter_secure_storage` v9+ uses `EncryptedSharedPreferences` by default when
`AndroidOptions(encryptedSharedPreferences: true)` is set. This relies on the
AndroidX Security Crypto library which wraps the Android Keystore. No extra
Gradle configuration is needed beyond `minSdk 23`.

If you need to migrate from the legacy keystore-backed implementation:

```dart
const storage = FlutterSecureStorage(
  aOptions: AndroidOptions(
    encryptedSharedPreferences: true,
    resetOnError: true, // Clears corrupted data instead of crashing.
  ),
);
```

---

## Biometric Authentication

Use the `local_auth` package to gate sensitive screens behind fingerprint or
face recognition.

```yaml
# pubspec.yaml
dependencies:
  local_auth: ^2.3.0
```

### Platform Configuration

**iOS** -- add to `ios/Runner/Info.plist`:

```xml
<key>NSFaceIDUsageDescription</key>
<string>Authenticate to access your secure data.</string>
```

**Android** -- add to `android/app/src/main/AndroidManifest.xml`:

```xml
<uses-permission android:name="android.permission.USE_BIOMETRIC"/>
```

### Biometric Service

```dart
// lib/core/security/biometric_service.dart

import 'package:local_auth/local_auth.dart';

/// Provides biometric capability detection and authentication prompts.
class BiometricService {
  BiometricService({LocalAuthentication? auth})
      : _auth = auth ?? LocalAuthentication();

  final LocalAuthentication _auth;

  /// Returns `true` when the device has enrolled biometrics **and** the
  /// hardware is available.
  Future<bool> get isAvailable async {
    final canCheck = await _auth.canCheckBiometrics;
    final isDeviceSupported = await _auth.isDeviceSupported();
    return canCheck && isDeviceSupported;
  }

  /// Lists enrolled biometric types (fingerprint, face, iris).
  Future<List<BiometricType>> get enrolledBiometrics =>
      _auth.getAvailableBiometrics();

  /// Prompts the user with a platform-native biometric dialog.
  ///
  /// Returns `true` when authentication succeeds.
  Future<bool> authenticate({
    String reason = 'Please authenticate to continue',
  }) async {
    try {
      return await _auth.authenticate(
        localizedReason: reason,
        options: const AuthenticationOptions(
          stickyAuth: true,
          biometricOnly: false, // Allow PIN/pattern as fallback.
        ),
      );
    } on Exception {
      return false;
    }
  }
}
```

### Guarding a Screen

```dart
// lib/features/vault/vault_screen.dart

import 'package:flutter/material.dart';

import '../../core/security/biometric_service.dart';

class VaultScreen extends StatefulWidget {
  const VaultScreen({super.key});

  @override
  State<VaultScreen> createState() => _VaultScreenState();
}

class _VaultScreenState extends State<VaultScreen> {
  final _biometric = BiometricService();
  bool _authenticated = false;

  @override
  void initState() {
    super.initState();
    _promptBiometric();
  }

  Future<void> _promptBiometric() async {
    final success = await _biometric.authenticate(
      reason: 'Unlock Vault to view sensitive data',
    );
    if (mounted) {
      setState(() => _authenticated = success);
    }
  }

  @override
  Widget build(BuildContext context) {
    if (!_authenticated) {
      return Scaffold(
        appBar: AppBar(title: const Text('Vault')),
        body: Center(
          child: ElevatedButton(
            onPressed: _promptBiometric,
            child: const Text('Authenticate'),
          ),
        ),
      );
    }

    return Scaffold(
      appBar: AppBar(title: const Text('Vault')),
      body: const Center(child: Text('Sensitive content visible.')),
    );
  }
}
```

---

## Secure Token Refresh Pattern

Combine `flutter_secure_storage` with a Dio interceptor so tokens are refreshed
transparently without exposing them to the UI layer.

```dart
// lib/core/network/auth_interceptor.dart

import 'dart:async';

import 'package:dio/dio.dart';

import '../security/secure_storage_service.dart';

/// Attaches the access token to every request and silently refreshes it
/// when the server responds with 401.
class AuthInterceptor extends QueuedInterceptor {
  AuthInterceptor({
    required SecureStorageService storage,
    required Dio tokenClient,
    required String refreshEndpoint,
  })  : _storage = storage,
        _tokenClient = tokenClient,
        _refreshEndpoint = refreshEndpoint;

  final SecureStorageService _storage;

  /// A **separate** Dio instance used only for the refresh call.
  /// Using the main instance would trigger this interceptor recursively.
  final Dio _tokenClient;

  final String _refreshEndpoint;

  // ── Attach access token ──────────────────────────────────────────────

  @override
  Future<void> onRequest(
    RequestOptions options,
    RequestInterceptorHandler handler,
  ) async {
    final token = await _storage.readAccessToken();
    if (token != null) {
      options.headers['Authorization'] = 'Bearer $token';
    }
    handler.next(options);
  }

  // ── Handle 401 ──────────────────────────────────────────────────────

  @override
  Future<void> onError(
    DioException err,
    ErrorInterceptorHandler handler,
  ) async {
    if (err.response?.statusCode != 401) {
      return handler.next(err);
    }

    try {
      final refreshToken = await _storage.readRefreshToken();
      if (refreshToken == null) {
        return handler.next(err);
      }

      final response = await _tokenClient.post<Map<String, dynamic>>(
        _refreshEndpoint,
        data: {'refresh_token': refreshToken},
      );

      final newAccess = response.data?['access_token'] as String?;
      final newRefresh = response.data?['refresh_token'] as String?;

      if (newAccess == null) {
        return handler.next(err);
      }

      await _storage.saveAccessToken(newAccess);
      if (newRefresh != null) {
        await _storage.saveRefreshToken(newRefresh);
      }

      // Retry the original request with the fresh token.
      final retryOptions = err.requestOptions
        ..headers['Authorization'] = 'Bearer $newAccess';

      final retryResponse = await _tokenClient.fetch<dynamic>(retryOptions);
      return handler.resolve(retryResponse);
    } on DioException {
      return handler.next(err);
    }
  }
}
```

### Wiring the Interceptor

```dart
// lib/core/network/api_client.dart

import 'package:dio/dio.dart';

import '../security/secure_storage_service.dart';
import 'auth_interceptor.dart';

Dio createApiClient({
  required String baseUrl,
  required SecureStorageService storage,
  required String refreshEndpoint,
}) {
  final tokenClient = Dio(BaseOptions(baseUrl: baseUrl));

  final dio = Dio(BaseOptions(
    baseUrl: baseUrl,
    connectTimeout: const Duration(seconds: 15),
    receiveTimeout: const Duration(seconds: 15),
  ));

  dio.interceptors.add(
    AuthInterceptor(
      storage: storage,
      tokenClient: tokenClient,
      refreshEndpoint: refreshEndpoint,
    ),
  );

  return dio;
}
```

---

## API Key Management

### Rule: Never hardcode secrets in Dart source.

Dart source is trivially decompilable. Any string literal -- even obfuscated --
can be extracted with basic tooling.

### Approach 1 -- `--dart-define` at build time

Pass secrets through the build command so they exist only in the compiled binary
and never in version-controlled source files.

```bash
# Build with injected keys (CI/CD pipeline)
flutter build apk \
  --dart-define=API_KEY=$API_KEY \
  --dart-define=API_SECRET=$API_SECRET \
  --obfuscate \
  --split-debug-info=build/symbols
```

Read them in Dart:

```dart
// lib/core/config/env.dart

/// Build-time environment variables injected via --dart-define.
///
/// These values are compiled into the binary and are NOT available at
/// runtime through any reflection API, but they can still be extracted
/// from an unobfuscated binary. Always combine with --obfuscate.
abstract final class Env {
  static const apiKey = String.fromEnvironment('API_KEY');
  static const apiSecret = String.fromEnvironment('API_SECRET');
}
```

### Approach 2 -- `envied` for type-safe .env

When you have many variables, `envied` generates an obfuscated Dart class from a
`.env` file.

```yaml
# pubspec.yaml
dependencies:
  envied: ^1.1.1

dev_dependencies:
  build_runner: ^2.4.14
  envied_generator: ^1.1.1
```

Create a `.env` file (add it to `.gitignore`):

```
API_KEY=sk-live-abc123
API_BASE_URL=https://api.example.com
```

Define the config class:

```dart
// lib/core/config/app_env.dart

import 'package:envied/envied.dart';

part 'app_env.g.dart';

@Envied(path: '.env', obfuscate: true)
abstract class AppEnv {
  @EnviedField(varName: 'API_KEY')
  static const String apiKey = _AppEnv.apiKey;

  @EnviedField(varName: 'API_BASE_URL')
  static const String apiBaseUrl = _AppEnv.apiBaseUrl;
}
```

Generate the code:

```bash
dart run build_runner build --delete-conflicting-outputs
```

The generated `app_env.g.dart` stores each character XOR-encrypted with a random
key, making casual string extraction ineffective.

### .gitignore Entries

```gitignore
# Secrets
.env
*.env
**/app_env.g.dart
```

---

## Clearing Sensitive Data on Logout

Every logout flow must erase tokens, cached credentials, and any in-memory
sensitive state.

```dart
// lib/core/security/session_manager.dart

import 'secure_storage_service.dart';

/// Orchestrates session lifecycle -- login persistence and secure teardown.
class SessionManager {
  SessionManager({required SecureStorageService storage}) : _storage = storage;

  final SecureStorageService _storage;

  /// Persist tokens received from the auth server.
  Future<void> saveSession({
    required String accessToken,
    required String refreshToken,
  }) async {
    await _storage.saveAccessToken(accessToken);
    await _storage.saveRefreshToken(refreshToken);
  }

  /// Returns `true` when a session exists (access token is present).
  Future<bool> get hasSession async =>
      (await _storage.readAccessToken()) != null;

  /// Destroy all stored credentials and sensitive data.
  ///
  /// Call this from your logout handler **before** navigating to the
  /// login screen.
  Future<void> destroySession() async {
    await _storage.clearAll();
    // If you cache anything in-memory (e.g., a user model singleton),
    // reset it here as well.
  }
}
```

### Integrating with a Logout Button

```dart
// In your settings or profile screen:

Future<void> _handleLogout(BuildContext context) async {
  final sessionManager = SessionManager(
    storage: SecureStorageService(),
  );

  await sessionManager.destroySession();

  if (context.mounted) {
    Navigator.of(context).pushNamedAndRemoveUntil('/login', (_) => false);
  }
}
```
