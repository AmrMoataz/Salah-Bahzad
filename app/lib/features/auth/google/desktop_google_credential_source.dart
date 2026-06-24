import 'package:firebase_auth/firebase_auth.dart';

import '../../../core/net/app_config.dart';
import '../identity_provider.dart';
import 'desktop_oauth_client.dart';
import 'google_credential_source.dart';

/// Windows Google sign-in: drive the system-browser OAuth loopback
/// ([DesktopOAuthClient]) to obtain Google tokens, then hand them to Firebase as
/// a [GoogleAuthProvider.credential] — identical to the plugin path from there.
///
/// The Google endpoints for an installed/desktop app:
class DesktopGoogleCredentialSource implements GoogleCredentialSource {
  DesktopGoogleCredentialSource({required this.config, required this.client});

  static const String _authority =
      'https://accounts.google.com/o/oauth2/v2/auth';
  static const String _tokenEndpoint = 'https://oauth2.googleapis.com/token';

  final AppConfig config;
  final DesktopOAuthClient client;

  @override
  Future<AuthCredential> getCredential() async {
    if (!config.hasDesktopGoogleOAuth) {
      throw IdentityException(
        'google_unconfigured',
        "Google sign-in isn't set up for desktop yet.",
      );
    }
    final OAuthTokens tokens = await client.authorize(
      authority: _authority,
      tokenEndpoint: _tokenEndpoint,
      clientId: config.googleDesktopClientId,
      clientSecret: config.googleDesktopClientSecret,
      scopes: config.googleScopes.split(' ')
        ..removeWhere((String s) => s.isEmpty),
    );
    return GoogleAuthProvider.credential(
      idToken: tokens.idToken,
      accessToken: tokens.accessToken,
    );
  }

  /// No `google_sign_in` plugin session exists on desktop — nothing to clear.
  @override
  Future<void> signOut() async {}
}
