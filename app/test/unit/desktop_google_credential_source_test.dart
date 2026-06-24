import 'package:firebase_auth/firebase_auth.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:secure_player/core/net/app_config.dart';
import 'package:secure_player/features/auth/google/desktop_google_credential_source.dart';
import 'package:secure_player/features/auth/google/desktop_oauth_client.dart';
import 'package:secure_player/features/auth/identity_provider.dart';

/// `DesktopGoogleCredentialSource` over a **fake** OAuth client — no browser,
/// socket, or Firebase (`NFR-APP-REL-003`). `GoogleAuthProvider.credential` is
/// a pure data object, so this exercises the seam end-to-end without Firebase
/// initialization.
class FakeOAuthClient implements DesktopOAuthClient {
  FakeOAuthClient({this.tokens, this.error});

  final OAuthTokens? tokens;
  final Object? error;
  List<String>? capturedScopes;
  String? capturedClientId;

  @override
  Future<OAuthTokens> authorize({
    required String authority,
    required String tokenEndpoint,
    required String clientId,
    required String clientSecret,
    required List<String> scopes,
  }) async {
    capturedScopes = scopes;
    capturedClientId = clientId;
    if (error != null) throw error!;
    return tokens!;
  }
}

AppConfig configWith({
  String clientId = 'cid',
  String scopes = 'openid email profile',
}) => AppConfig(
  apiBaseUrl: 'http://localhost',
  appVersion: '1.0.0',
  portalUrl: 'http://localhost',
  googleDesktopClientId: clientId,
  googleDesktopClientSecret: 'secret',
  googleScopes: scopes,
);

void main() {
  test('configured + tokens → Google AuthCredential', () async {
    final FakeOAuthClient client = FakeOAuthClient(
      tokens: const OAuthTokens(idToken: 'google-id', accessToken: 'google-at'),
    );
    final DesktopGoogleCredentialSource source = DesktopGoogleCredentialSource(
      config: configWith(),
      client: client,
    );

    final AuthCredential cred = await source.getCredential();

    expect(cred.providerId, 'google.com');
    expect(cred.accessToken, 'google-at');
    expect(client.capturedClientId, 'cid');
    // Scopes are split on spaces, empties dropped.
    expect(client.capturedScopes, <String>['openid', 'email', 'profile']);
  });

  test('unconfigured (empty client id) → google_unconfigured', () async {
    final DesktopGoogleCredentialSource source = DesktopGoogleCredentialSource(
      config: configWith(clientId: ''),
      client: FakeOAuthClient(tokens: const OAuthTokens(idToken: 'x')),
    );
    expect(
      source.getCredential(),
      throwsA(
        isA<IdentityException>().having(
          (IdentityException e) => e.code,
          'code',
          'google_unconfigured',
        ),
      ),
    );
  });

  test(
    'client cancel/timeout IdentityException propagates unchanged',
    () async {
      final DesktopGoogleCredentialSource source =
          DesktopGoogleCredentialSource(
            config: configWith(),
            client: FakeOAuthClient(
              error: IdentityException('google_timeout', 'timed out'),
            ),
          );
      expect(
        source.getCredential(),
        throwsA(
          isA<IdentityException>().having(
            (IdentityException e) => e.code,
            'code',
            'google_timeout',
          ),
        ),
      );
    },
  );

  test('signOut is a no-op on desktop (no plugin session)', () async {
    final DesktopGoogleCredentialSource source = DesktopGoogleCredentialSource(
      config: configWith(),
      client: FakeOAuthClient(tokens: const OAuthTokens(idToken: 'x')),
    );
    await expectLater(source.signOut(), completes);
  });
}
