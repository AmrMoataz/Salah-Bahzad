# Secure Storage

This guide covers storing sensitive data (auth tokens, API keys, biometric
secrets) using platform keychains, as well as SharedPreferences for
non-sensitive user preferences.

## Table of Contents

1. [flutter_secure_storage Setup](#flutter_secure_storage-setup)
2. [Storing Auth Tokens Securely](#storing-auth-tokens-securely)
3. [Biometric-Protected Storage](#biometric-protected-storage)
4. [Clearing on Logout](#clearing-on-logout)
5. [Platform-Specific Configuration](#platform-specific-configuration)
6. [SharedPreferences for Non-Sensitive Data](#sharedpreferences-for-non-sensitive-data)
7. [Decision Table: When to Use Which](#decision-table-when-to-use-which)

---

## flutter_secure_storage Setup

### pubspec.yaml

```yaml
dependencies:
  flutter_secure_storage: ^9.2.4
```

### Platform Requirements

| Platform | Backend                        | Minimum Version    |
|----------|--------------------------------|--------------------|
| iOS      | Keychain Services              | iOS 12+            |
| Android  | EncryptedSharedPreferences      | API 23 (Android 6) |
| macOS    | Keychain Services              | macOS 10.15+       |
| Linux    | libsecret                      | Any                |
| Windows  | Windows Credentials API (Wincred) | Windows 10+     |
| Web      | Not truly secure (localStorage)| N/A                |

### Android Setup

In `android/app/build.gradle`:

```groovy
android {
    defaultConfig {
        minSdk = 23 // Required for EncryptedSharedPreferences
    }
}
```

In `android/app/src/main/AndroidManifest.xml` (only if using biometrics):

```xml
<uses-permission android:name="android.permission.USE_BIOMETRIC" />
```

### iOS Setup

No additional configuration is needed for basic Keychain usage. For biometric
protection, add to `ios/Runner/Info.plist`:

```xml
<key>NSFaceIDUsageDescription</key>
<string>We use Face ID to protect your sensitive data.</string>
```

---

## Storing Auth Tokens Securely

### Token Storage Service

```dart
// lib/core/auth/token_storage.dart

import 'package:flutter_secure_storage/flutter_secure_storage.dart';

class TokenStorage {
  static const _accessTokenKey = 'auth_access_token';
  static const _refreshTokenKey = 'auth_refresh_token';
  static const _tokenExpiryKey = 'auth_token_expiry';

  final FlutterSecureStorage _storage;

  TokenStorage({FlutterSecureStorage? storage})
      : _storage = storage ??
            const FlutterSecureStorage(
              aOptions: AndroidOptions(encryptedSharedPreferences: true),
              iOptions: IOSOptions(
                accessibility: KeychainAccessibility.first_unlock_this_device,
              ),
            );

  // --- Write ---

  Future<void> saveTokens({
    required String accessToken,
    required String refreshToken,
    required DateTime expiry,
  }) async {
    await Future.wait([
      _storage.write(key: _accessTokenKey, value: accessToken),
      _storage.write(key: _refreshTokenKey, value: refreshToken),
      _storage.write(
        key: _tokenExpiryKey,
        value: expiry.toIso8601String(),
      ),
    ]);
  }

  // --- Read ---

  Future<String?> get accessToken =>
      _storage.read(key: _accessTokenKey);

  Future<String?> get refreshToken =>
      _storage.read(key: _refreshTokenKey);

  Future<DateTime?> get tokenExpiry async {
    final raw = await _storage.read(key: _tokenExpiryKey);
    return raw != null ? DateTime.tryParse(raw) : null;
  }

  Future<bool> get hasValidToken async {
    final token = await accessToken;
    final expiry = await tokenExpiry;
    if (token == null || expiry == null) return false;
    return expiry.isAfter(DateTime.now());
  }

  // --- Delete ---

  Future<void> clearTokens() async {
    await Future.wait([
      _storage.delete(key: _accessTokenKey),
      _storage.delete(key: _refreshTokenKey),
      _storage.delete(key: _tokenExpiryKey),
    ]);
  }
}
```

### Using TokenStorage in an Auth Interceptor

```dart
// lib/core/network/auth_interceptor.dart

import 'package:dio/dio.dart';

import '../auth/token_storage.dart';

class AuthInterceptor extends Interceptor {
  final TokenStorage _tokenStorage;
  final Dio _dio; // Separate Dio instance for refresh requests.

  AuthInterceptor({
    required TokenStorage tokenStorage,
    required Dio refreshDio,
  })  : _tokenStorage = tokenStorage,
        _dio = refreshDio;

  @override
  Future<void> onRequest(
    RequestOptions options,
    RequestInterceptorHandler handler,
  ) async {
    final token = await _tokenStorage.accessToken;
    if (token != null) {
      options.headers['Authorization'] = 'Bearer $token';
    }
    handler.next(options);
  }

  @override
  Future<void> onError(
    DioException err,
    ErrorInterceptorHandler handler,
  ) async {
    if (err.response?.statusCode != 401) {
      return handler.next(err);
    }

    // Attempt token refresh.
    final refreshToken = await _tokenStorage.refreshToken;
    if (refreshToken == null) {
      return handler.next(err);
    }

    try {
      final response = await _dio.post(
        '/auth/refresh',
        data: {'refresh_token': refreshToken},
      );

      final newAccess = response.data['access_token'] as String;
      final newRefresh = response.data['refresh_token'] as String;
      final expiresIn = response.data['expires_in'] as int;

      await _tokenStorage.saveTokens(
        accessToken: newAccess,
        refreshToken: newRefresh,
        expiry: DateTime.now().add(Duration(seconds: expiresIn)),
      );

      // Retry the original request with the new token.
      final retryOptions = err.requestOptions
        ..headers['Authorization'] = 'Bearer $newAccess';
      final retryResponse = await _dio.fetch(retryOptions);
      handler.resolve(retryResponse);
    } catch (_) {
      await _tokenStorage.clearTokens();
      handler.next(err);
    }
  }
}
```

---

## Biometric-Protected Storage

Require a fingerprint or face scan before reading a value.

```dart
// lib/core/auth/biometric_storage.dart

import 'package:flutter_secure_storage/flutter_secure_storage.dart';

class BiometricStorage {
  static const _pinKey = 'user_pin';

  final FlutterSecureStorage _storage;

  BiometricStorage()
      : _storage = const FlutterSecureStorage(
          aOptions: AndroidOptions(
            encryptedSharedPreferences: true,
          ),
          iOptions: IOSOptions(
            accessibility:
                KeychainAccessibility.when_passcode_set_this_device_only,
          ),
        );

  /// Writes a value that will require biometric auth to read on iOS.
  Future<void> savePin(String pin) async {
    await _storage.write(
      key: _pinKey,
      value: pin,
      iOptions: const IOSOptions(
        accessibility:
            KeychainAccessibility.when_passcode_set_this_device_only,
        // Setting useAccessControlForBiometrics triggers the biometric prompt
        // the next time the value is read.
      ),
      aOptions: const AndroidOptions(
        encryptedSharedPreferences: true,
      ),
    );
  }

  /// Reads the PIN. On iOS this may trigger a Face ID / Touch ID prompt.
  Future<String?> readPin() async {
    try {
      return await _storage.read(
        key: _pinKey,
        iOptions: const IOSOptions(
          accessibility:
              KeychainAccessibility.when_passcode_set_this_device_only,
        ),
        aOptions: const AndroidOptions(
          encryptedSharedPreferences: true,
        ),
      );
    } catch (e) {
      // Biometric auth was cancelled or failed.
      return null;
    }
  }

  Future<void> deletePin() async {
    await _storage.delete(key: _pinKey);
  }
}
```

### Using local_auth for Explicit Biometric Gates

For an explicit "authenticate before proceeding" flow (independent of keychain
access), use `local_auth`:

```yaml
dependencies:
  local_auth: ^2.3.0
```

```dart
// lib/core/auth/biometric_auth_service.dart

import 'package:local_auth/local_auth.dart';

class BiometricAuthService {
  final LocalAuthentication _auth;

  BiometricAuthService({LocalAuthentication? auth})
      : _auth = auth ?? LocalAuthentication();

  /// Returns true if the device supports biometric authentication.
  Future<bool> get isAvailable async {
    final canCheck = await _auth.canCheckBiometrics;
    final isDeviceSupported = await _auth.isDeviceSupported();
    return canCheck && isDeviceSupported;
  }

  /// Returns available biometric types (fingerprint, face, iris).
  Future<List<BiometricType>> get availableTypes =>
      _auth.getAvailableBiometrics();

  /// Prompts the user to authenticate.
  Future<bool> authenticate({
    String reason = 'Please authenticate to continue.',
  }) async {
    try {
      return await _auth.authenticate(
        localizedReason: reason,
        options: const AuthenticationOptions(
          stickyAuth: true,
          biometricOnly: false, // Allow PIN/password fallback.
        ),
      );
    } catch (_) {
      return false;
    }
  }
}
```

### Gating a Screen with Biometrics

```dart
import 'package:flutter/material.dart';

import '../../core/auth/biometric_auth_service.dart';

class SecureScreen extends StatefulWidget {
  final BiometricAuthService authService;

  const SecureScreen({super.key, required this.authService});

  @override
  State<SecureScreen> createState() => _SecureScreenState();
}

class _SecureScreenState extends State<SecureScreen> {
  bool _authenticated = false;
  bool _checking = true;

  @override
  void initState() {
    super.initState();
    _promptBiometric();
  }

  Future<void> _promptBiometric() async {
    final success = await widget.authService.authenticate(
      reason: 'Authenticate to view sensitive data.',
    );
    if (mounted) {
      setState(() {
        _authenticated = success;
        _checking = false;
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    if (_checking) {
      return const Scaffold(
        body: Center(child: CircularProgressIndicator()),
      );
    }

    if (!_authenticated) {
      return Scaffold(
        body: Center(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              const Text('Authentication required.'),
              const SizedBox(height: 16),
              FilledButton(
                onPressed: _promptBiometric,
                child: const Text('Try Again'),
              ),
            ],
          ),
        ),
      );
    }

    return Scaffold(
      appBar: AppBar(title: const Text('Secure Data')),
      body: const Center(child: Text('You are authenticated.')),
    );
  }
}
```

---

## Clearing on Logout

```dart
// lib/core/auth/logout_service.dart

import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import 'package:hive_flutter/hive_flutter.dart';
import 'package:shared_preferences/shared_preferences.dart';

class LogoutService {
  final FlutterSecureStorage _secureStorage;

  LogoutService({FlutterSecureStorage? secureStorage})
      : _secureStorage = secureStorage ?? const FlutterSecureStorage();

  /// Clears all sensitive data. Call on explicit logout.
  Future<void> logout() async {
    // 1. Clear auth tokens from secure storage.
    await _secureStorage.deleteAll();

    // 2. Clear user-specific Hive boxes.
    if (Hive.isBoxOpen('userSettings')) {
      await Hive.box('userSettings').clear();
    }
    if (Hive.isBoxOpen('articleCache')) {
      await Hive.box('articleCache').clear();
    }

    // 3. Clear user-specific SharedPreferences keys.
    //    Do NOT call prefs.clear() if you store install-scoped
    //    values like onboarding-complete or device ID.
    final prefs = await SharedPreferences.getInstance();
    await prefs.remove('user_id');
    await prefs.remove('user_email');
    await prefs.remove('last_sync_time');

    // 4. (Optional) Close and delete Hive boxes from disk.
    // await Hive.deleteBoxFromDisk('articleCache');
  }

  /// Nuclear option: removes ALL local data. Useful for "delete my account".
  Future<void> deleteAllData() async {
    await _secureStorage.deleteAll();

    final prefs = await SharedPreferences.getInstance();
    await prefs.clear();

    // Close all Hive boxes before deleting.
    await Hive.close();
    await Hive.deleteFromDisk();
  }
}
```

---

## Platform-Specific Configuration

### AndroidOptions

```dart
const androidOptions = AndroidOptions(
  // Use EncryptedSharedPreferences (API 23+). Recommended.
  encryptedSharedPreferences: true,

  // Optional: set a custom shared preferences name.
  // sharedPreferencesName: 'my_secure_prefs',

  // Optional: set a custom prefix for keys.
  // preferencesKeyPrefix: 'secure_',

  // Reset data on error (e.g., after OS key rotation).
  resetOnError: true,
);

const storage = FlutterSecureStorage(aOptions: androidOptions);
```

### IOSOptions

```dart
const iosOptions = IOSOptions(
  // Controls when the keychain item is accessible.
  // Options:
  //   .passcode                  -- device must have a passcode
  //   .unlocked                  -- device must be unlocked
  //   .unlocked_this_device_only -- unlocked + not transferable via backup
  //   .first_unlock              -- accessible after first unlock since boot
  //   .first_unlock_this_device  -- first unlock + not transferable
  //   .when_passcode_set_this_device_only -- requires passcode, device-bound
  accessibility: KeychainAccessibility.first_unlock_this_device,

  // Group ID for sharing keychain items across apps from the same developer.
  // groupId: 'group.com.example.shared',

  // Account name for the keychain item.
  // accountName: 'MyApp',

  // Synchronize to iCloud Keychain (default: false).
  // synchronizable: false,
);

const storage = FlutterSecureStorage(iOptions: iosOptions);
```

### LinuxOptions

```dart
const linuxOptions = LinuxOptions(
  // Uses libsecret. No special configuration needed in most cases.
);

const storage = FlutterSecureStorage(lOptions: linuxOptions);
```

### WindowsOptions

```dart
const windowsOptions = WindowsOptions(
  // Uses Windows Credential Locker.
  // useBackwardCompatibility: false, // Migrate from old storage format.
);

const storage = FlutterSecureStorage(wOptions: windowsOptions);
```

### Web Caveat

On Web, `flutter_secure_storage` falls back to `window.localStorage`, which
is **not encrypted**. For web-only apps, consider:

- HttpOnly cookies (managed server-side) for auth tokens.
- IndexedDB with application-level encryption for larger datasets.
- Accepting the risk for non-sensitive preferences.

---

## SharedPreferences for Non-Sensitive Data

SharedPreferences is the simplest key-value store. Use it for user preferences,
feature flags, and non-sensitive settings.

### pubspec.yaml

```yaml
dependencies:
  shared_preferences: ^2.3.5
```

### Preferences Service

```dart
// lib/core/preferences/preferences_service.dart

import 'package:shared_preferences/shared_preferences.dart';

class PreferencesService {
  final SharedPreferences _prefs;

  PreferencesService(this._prefs);

  /// Factory that loads SharedPreferences asynchronously.
  static Future<PreferencesService> create() async {
    final prefs = await SharedPreferences.getInstance();
    return PreferencesService(prefs);
  }

  // --- Theme ---

  static const _darkModeKey = 'pref_dark_mode';

  bool get isDarkMode => _prefs.getBool(_darkModeKey) ?? false;

  Future<void> setDarkMode(bool value) =>
      _prefs.setBool(_darkModeKey, value);

  // --- Locale ---

  static const _localeKey = 'pref_locale';

  String get locale => _prefs.getString(_localeKey) ?? 'en';

  Future<void> setLocale(String value) =>
      _prefs.setString(_localeKey, value);

  // --- Onboarding ---

  static const _onboardingKey = 'pref_onboarding_complete';

  bool get isOnboardingComplete =>
      _prefs.getBool(_onboardingKey) ?? false;

  Future<void> completeOnboarding() =>
      _prefs.setBool(_onboardingKey, true);

  // --- Feature Flags ---

  static const _featureFlagPrefix = 'ff_';

  bool featureFlag(String flag) =>
      _prefs.getBool('$_featureFlagPrefix$flag') ?? false;

  Future<void> setFeatureFlag(String flag, bool enabled) =>
      _prefs.setBool('$_featureFlagPrefix$flag', enabled);

  // --- Cache Metadata ---

  static const _lastSyncKey = 'last_sync_time';

  DateTime? get lastSyncTime {
    final ms = _prefs.getInt(_lastSyncKey);
    return ms != null ? DateTime.fromMillisecondsSinceEpoch(ms) : null;
  }

  Future<void> setLastSyncTime(DateTime time) =>
      _prefs.setInt(_lastSyncKey, time.millisecondsSinceEpoch);

  // --- Typed List ---

  static const _recentSearchesKey = 'recent_searches';

  List<String> get recentSearches =>
      _prefs.getStringList(_recentSearchesKey) ?? [];

  Future<void> addRecentSearch(String query) async {
    final searches = recentSearches;
    searches.remove(query); // deduplicate
    searches.insert(0, query);
    if (searches.length > 20) searches.removeLast();
    await _prefs.setStringList(_recentSearchesKey, searches);
  }

  Future<void> clearRecentSearches() =>
      _prefs.remove(_recentSearchesKey);
}
```

### Testing SharedPreferences

```dart
import 'package:flutter_test/flutter_test.dart';
import 'package:shared_preferences/shared_preferences.dart';

import 'package:my_app/core/preferences/preferences_service.dart';

void main() {
  late PreferencesService service;

  setUp(() async {
    // Set initial values for testing.
    SharedPreferences.setMockInitialValues({});
    final prefs = await SharedPreferences.getInstance();
    service = PreferencesService(prefs);
  });

  test('dark mode defaults to false', () {
    expect(service.isDarkMode, false);
  });

  test('setting dark mode persists', () async {
    await service.setDarkMode(true);
    expect(service.isDarkMode, true);
  });

  test('recent searches maintains order and limit', () async {
    for (var i = 0; i < 25; i++) {
      await service.addRecentSearch('query_$i');
    }
    final searches = service.recentSearches;
    expect(searches.length, 20);
    expect(searches.first, 'query_24');
    expect(searches.last, 'query_5');
  });

  test('duplicate search moves to front', () async {
    await service.addRecentSearch('flutter');
    await service.addRecentSearch('dart');
    await service.addRecentSearch('flutter');

    final searches = service.recentSearches;
    expect(searches, ['flutter', 'dart']);
  });
}
```

---

## Decision Table: When to Use Which

| Data Type                     | Storage Solution           | Why                                                        |
|-------------------------------|----------------------------|------------------------------------------------------------|
| Auth tokens (JWT, OAuth)      | `flutter_secure_storage`   | Encrypted by platform keychain; never in plaintext         |
| API keys                      | `flutter_secure_storage`   | Must not be accessible to other apps or visible in backups |
| Refresh tokens                | `flutter_secure_storage`   | Long-lived credentials must be protected at rest           |
| User PIN / password hash      | `flutter_secure_storage`   | Sensitive authentication material                          |
| Biometric-gated secrets       | `flutter_secure_storage` + `local_auth` | Combines keychain encryption with biometric gate  |
| Theme preference              | `SharedPreferences`        | Simple boolean; not sensitive                              |
| Locale / language             | `SharedPreferences`        | Simple string; not sensitive                               |
| Onboarding complete flag      | `SharedPreferences`        | Simple boolean; not sensitive                              |
| Feature flags                 | `SharedPreferences`        | Simple booleans; fetched from remote config                |
| Last sync timestamp           | `SharedPreferences`        | Simple integer; used for cache invalidation                |
| Recent search queries         | `SharedPreferences`        | String list; small dataset; not sensitive                  |
| Structured app data (relational) | Drift (SQLite)          | Needs joins, indexes, complex queries, migrations          |
| Cached API responses (documents) | Hive or Isar            | Fast read/write; flexible schema; optional encryption      |
| Full-text searchable content  | Isar or Drift (FTS5)       | Built-in indexing and search capabilities                  |
| Large binary blobs            | File system + `path_provider` | Databases are not optimized for large blobs             |
| Encrypted local notes / PII   | Hive (encrypted box)       | At-rest encryption for non-credential personal data        |
