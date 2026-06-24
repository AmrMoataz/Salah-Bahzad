import 'package:firebase_auth/firebase_auth.dart';
import 'package:google_sign_in/google_sign_in.dart';

import '../identity_provider.dart';

/// Produces a Google [AuthCredential] for Firebase, abstracting *how* the Google
/// `idToken`/`accessToken` are obtained so the platform difference is the only
/// thing that varies. [FirebaseIdentityProvider] feeds the result straight into
/// `signInWithCredential` — one Firebase path for every platform.
///
/// Two implementations:
/// - [PluginGoogleCredentialSource] — `google_sign_in` (android/ios/macOS).
/// - `DesktopGoogleCredentialSource` — system-browser OAuth loopback (windows).
///
/// Provider-generic by design: Apple/Microsoft later are a second source over
/// the same desktop OAuth client + the matching `OAuthProvider`.
abstract class GoogleCredentialSource {
  /// Run the Google account picker / consent and return a Firebase credential.
  Future<AuthCredential> getCredential();

  /// Clear any provider-side session this source owns (plugin platforms have a
  /// signed-in `google_sign_in` account; desktop has nothing to clear). Must
  /// never throw out — failures here cannot block clearing the app session.
  Future<void> signOut();
}

/// Wraps `google_sign_in` v7 (android/ios/macOS). This is the existing mobile
/// flow lifted out of `FirebaseIdentityProvider` byte-for-byte — initialize →
/// authenticate → `GoogleAuthProvider.credential` — so behavior there is
/// unchanged.
class PluginGoogleCredentialSource implements GoogleCredentialSource {
  PluginGoogleCredentialSource({GoogleSignIn? googleSignIn})
    : _google = googleSignIn ?? GoogleSignIn.instance;

  final GoogleSignIn _google;
  bool _googleInitialized = false;

  @override
  Future<AuthCredential> getCredential() async {
    try {
      if (!_googleInitialized) {
        await _google.initialize();
        _googleInitialized = true;
      }
      final GoogleSignInAccount account = await _google.authenticate();
      final String? idToken = account.authentication.idToken;
      if (idToken == null) {
        throw IdentityException('google_no_token', 'Google sign-in failed.');
      }
      return GoogleAuthProvider.credential(idToken: idToken);
    } on GoogleSignInException catch (e) {
      throw IdentityException(
        'google_${e.code.name}',
        'Google sign-in failed.',
      );
    }
  }

  @override
  Future<void> signOut() => _google.signOut();
}
