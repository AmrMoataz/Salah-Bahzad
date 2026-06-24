/// Runtime configuration, supplied at build/run time via `--dart-define`.
///
/// Nothing secret lives here — only the API base URL, the app's semver (sent as
/// `X-App-Version`, contract §G/§F) and the portal URL the Idle screen opens.
class AppConfig {
  const AppConfig({
    required this.apiBaseUrl,
    required this.appVersion,
    required this.portalUrl,
  });

  final String apiBaseUrl;
  final String appVersion;
  final String portalUrl;

  /// Built from `--dart-define`s with dev-friendly defaults so the app runs
  /// against a local Aspire stack out of the box.
  factory AppConfig.fromEnvironment() {
    return const AppConfig(
      apiBaseUrl: String.fromEnvironment(
        'API_BASE_URL',
        defaultValue: 'https://localhost:5010',
      ),
      appVersion: String.fromEnvironment('APP_VERSION', defaultValue: '1.0.0'),
      portalUrl: String.fromEnvironment(
        'PORTAL_URL',
        defaultValue: 'https://localhost:56092',
      ),
    );
  }
}
