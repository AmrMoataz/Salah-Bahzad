/// Runtime configuration, supplied at build/run time via `--dart-define`.
///
/// Nothing secret lives here — only the API base URL, the app's semver (sent as
/// `X-App-Version`, contract §G/§F), the portal URL the Idle screen opens, and
/// the desktop Google OAuth client used for Windows Google sign-in. (For a
/// Google "Desktop app" OAuth client the secret is **not** a true secret —
/// Google's installed-app model embeds it in the distributed client — but it's
/// still injected via config, never hardcoded.)
class AppConfig {
  const AppConfig({
    required this.apiBaseUrl,
    required this.appVersion,
    required this.portalUrl,
    this.googleDesktopClientId = '',
    this.googleDesktopClientSecret = '',
    this.googleScopes = 'openid email profile',
  });

  final String apiBaseUrl;
  final String appVersion;
  final String portalUrl;

  /// Google "Desktop app" OAuth client id/secret for the Windows system-browser
  /// loopback flow (§G-Setup). Empty on platforms/builds that don't configure
  /// it — Google sign-in is then offered only where the `google_sign_in` plugin
  /// ships (mobile/macOS).
  final String googleDesktopClientId;
  final String googleDesktopClientSecret;

  /// Space-delimited OAuth scopes for the desktop flow.
  final String googleScopes;

  /// Whether the Windows desktop Google OAuth flow is configured.
  bool get hasDesktopGoogleOAuth => googleDesktopClientId.isNotEmpty;

  /// Built from `--dart-define`s with dev-friendly defaults so the app runs
  /// against a local Aspire stack out of the box.
  ///
  /// The dev default targets the AppHost's **stable, named `app` HTTP endpoint**
  /// (`WithHttpEndpoint(port: 5080, name: "app")` in `SalahBahazad.AppHost`) —
  /// NOT `:5010` (that port is Aspire's DCP control plane) and NOT the API's
  /// dynamic proxied port (which Aspire reassigns every run). Dev is HTTP on
  /// purpose: Dart/Flutter doesn't read the OS cert store, so the ASP.NET dev
  /// cert can't be trusted client-side. Production passes
  /// `--dart-define=API_BASE_URL=https://…` (a real CA cert Dart trusts).
  factory AppConfig.fromEnvironment() {
    return const AppConfig(
      apiBaseUrl: String.fromEnvironment(
        'API_BASE_URL',
        defaultValue: 'http://localhost:5080',
      ),
      appVersion: String.fromEnvironment('APP_VERSION', defaultValue: '1.0.0'),
      portalUrl: String.fromEnvironment(
        'PORTAL_URL',
        defaultValue: 'https://localhost:56092',
      ),
      googleDesktopClientId: String.fromEnvironment('GOOGLE_DESKTOP_CLIENT_ID'),
      googleDesktopClientSecret: String.fromEnvironment(
        'GOOGLE_DESKTOP_CLIENT_SECRET',
      ),
      googleScopes: String.fromEnvironment(
        'GOOGLE_SCOPES',
        defaultValue: 'openid email profile',
      ),
    );
  }
}
