import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';

import '../core/deeplink/deep_link_service.dart';
import '../core/deeplink/pending_deep_link.dart';
import '../core/logging/logging.dart';
import '../core/net/api_client.dart';
import '../core/net/app_config.dart';
import '../core/net/token_refresher.dart';
import '../core/platform/app_platform.dart';
import '../core/storage/session_store.dart';
import '../data/auth_repository.dart';
import '../features/auth/auth_controller.dart';
import '../features/auth/auth_state.dart';
import '../features/auth/google/desktop_google_credential_source.dart';
import '../features/auth/google/desktop_oauth_client.dart';
import '../features/auth/google/google_credential_source.dart';
import '../features/auth/identity_provider.dart';

// ── Diagnostics ─────────────────────────────────────────────────────────────

/// The app logger, wired to whatever sinks `main.dart` configured (console
/// today; Sentry in A3). Inject and `.scoped('…')` it; override in tests with a
/// capturing logger to assert on emitted records.
final loggerProvider = Provider<AppLogger>((ref) => Log.root);

// ── Configuration & platform ────────────────────────────────────────────────

final appConfigProvider = Provider<AppConfig>(
  (ref) => AppConfig.fromEnvironment(),
);

final appPlatformProvider = Provider<AppPlatform>((ref) => AppPlatform());

// ── Storage ─────────────────────────────────────────────────────────────────

final secureStorageProvider = Provider<FlutterSecureStorage>(
  (ref) => const FlutterSecureStorage(),
);

final sessionStoreProvider = Provider<SessionStore>(
  (ref) => SessionStore(ref.read(secureStorageProvider)),
);

// ── Networking ──────────────────────────────────────────────────────────────

/// A **bare** Dio (no auth/refresh interceptor) used only by [TokenRefresher],
/// so a 401 during refresh can never recurse.
final bareDioProvider = Provider<Dio>((ref) {
  final AppConfig config = ref.read(appConfigProvider);
  final AppPlatform platform = ref.read(appPlatformProvider);
  return Dio(
    BaseOptions(
      baseUrl: config.apiBaseUrl,
      connectTimeout: const Duration(seconds: 15),
      receiveTimeout: const Duration(seconds: 30),
      headers: <String, dynamic>{
        'X-App-Version': config.appVersion,
        'X-Platform': platform.target.wireName,
        'Accept': 'application/json',
      },
    ),
  );
});

final tokenRefresherProvider = Provider<TokenRefresher>(
  (ref) => TokenRefresher(ref.read(bareDioProvider)),
);

final apiClientProvider = Provider<ApiClient>((ref) {
  final ApiClient client = ApiClient(
    config: ref.read(appConfigProvider),
    store: ref.read(sessionStoreProvider),
    refresher: ref.read(tokenRefresherProvider),
    platform: ref.read(appPlatformProvider),
  );
  ref.onDispose(client.dispose);
  return client;
});

final authRepositoryProvider = Provider<AuthRepository>(
  (ref) => AuthRepository(ref.read(apiClientProvider)),
);

// ── Identity (Firebase / Google) ────────────────────────────────────────────

/// Picks how a Google credential is produced: the `google_sign_in` plugin on
/// mobile/macOS, or the system-browser OAuth loopback on Windows.
final googleCredentialSourceProvider = Provider<GoogleCredentialSource>((ref) {
  final AppPlatform platform = ref.read(appPlatformProvider);
  if (platform.isWindows) {
    return DesktopGoogleCredentialSource(
      config: ref.read(appConfigProvider),
      client: SystemBrowserOAuthClient(),
    );
  }
  return PluginGoogleCredentialSource();
});

final identityProvider = Provider<IdentityProvider>((ref) {
  final AppPlatform platform = ref.read(appPlatformProvider);
  final AppConfig config = ref.read(appConfigProvider);
  return FirebaseIdentityProvider(
    googleSupported: FirebaseIdentityProvider.computeGoogleSupported(
      isWindows: platform.isWindows,
      hasDesktopGoogleOAuth: config.hasDesktopGoogleOAuth,
    ),
    googleSource: ref.read(googleCredentialSourceProvider),
  );
});

// ── Auth machine ────────────────────────────────────────────────────────────

final authControllerProvider = NotifierProvider<AuthController, AuthState>(
  AuthController.new,
);

// ── Deep links ──────────────────────────────────────────────────────────────

final deepLinkServiceProvider = Provider<DeepLinkService>(
  (ref) => DeepLinkService(),
);

final pendingDeepLinkProvider =
    NotifierProvider<PendingDeepLinkController, PendingDeepLink?>(
      PendingDeepLinkController.new,
    );
