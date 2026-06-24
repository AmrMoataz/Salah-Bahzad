import 'package:firebase_auth/firebase_auth.dart';
import 'package:google_sign_in/google_sign_in.dart';

import '../../core/platform/app_platform.dart';

/// A failure raised by the identity layer (Firebase / Google), distinct from the
/// backend's `app-exchange` errors (which are [ApiException]s). Carries a stable
/// [code] and a human [message] the Sign-in screen can show inline.
class IdentityException implements Exception {
  IdentityException(this.code, this.message);

  final String code;
  final String message;

  @override
  String toString() => 'IdentityException($code)';
}

/// The Firebase/Google sign-in seam. Abstracted so the [AuthController] is
/// testable with a fake (the dev/test path never touches Firebase —
/// `NFR-APP-REL-003`). Implementations return a **Firebase ID token**, which the
/// app then trades for a platform session via `app-exchange`.
abstract class IdentityProvider {
  /// Whether "Continue with Google" is offered on this platform (mobile/macOS
  /// only — `google_sign_in` does not ship for Windows).
  bool get googleSupported;

  Future<String> signInWithEmailPassword({
    required String email,
    required String password,
  });

  Future<String> signInWithGoogle();

  Future<void> signOut();
}

/// Real implementation over `firebase_auth` + `google_sign_in` v7.
class FirebaseIdentityProvider implements IdentityProvider {
  FirebaseIdentityProvider({
    required AppPlatform platform,
    FirebaseAuth? auth,
    GoogleSignIn? googleSignIn,
  })  : _googleSupported = platform.googleSignInSupported,
        _auth = auth ?? FirebaseAuth.instance,
        _google = googleSignIn ?? GoogleSignIn.instance;

  final bool _googleSupported;
  final FirebaseAuth _auth;
  final GoogleSignIn _google;
  bool _googleInitialized = false;

  @override
  bool get googleSupported => _googleSupported;

  @override
  Future<String> signInWithEmailPassword({
    required String email,
    required String password,
  }) async {
    try {
      final UserCredential cred = await _auth.signInWithEmailAndPassword(
        email: email.trim(),
        password: password,
      );
      return _idTokenOf(cred);
    } on FirebaseAuthException catch (e) {
      throw IdentityException(e.code, _friendly(e.code));
    }
  }

  @override
  Future<String> signInWithGoogle() async {
    if (!googleSupported) {
      throw IdentityException(
        'google_unsupported',
        'Google sign-in is not available on this device.',
      );
    }
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
      final OAuthCredential credential =
          GoogleAuthProvider.credential(idToken: idToken);
      final UserCredential cred = await _auth.signInWithCredential(credential);
      return _idTokenOf(cred);
    } on FirebaseAuthException catch (e) {
      throw IdentityException(e.code, _friendly(e.code));
    } on GoogleSignInException catch (e) {
      throw IdentityException('google_${e.code.name}', 'Google sign-in failed.');
    }
  }

  @override
  Future<void> signOut() async {
    await _auth.signOut();
    if (googleSupported) {
      try {
        await _google.signOut();
      } catch (_) {
        // A Google sign-out failure must not block clearing the app session.
      }
    }
  }

  Future<String> _idTokenOf(UserCredential cred) async {
    final String? token = await cred.user?.getIdToken();
    if (token == null) {
      throw IdentityException('no_id_token', 'Could not verify your identity.');
    }
    return token;
  }

  String _friendly(String code) {
    switch (code) {
      case 'invalid-credential':
      case 'wrong-password':
      case 'user-not-found':
      case 'invalid-email':
        return 'Wrong email or password.';
      case 'user-disabled':
        return 'This account has been disabled.';
      case 'too-many-requests':
        return 'Too many attempts. Try again in a moment.';
      case 'network-request-failed':
        return 'Network error. Check your connection.';
      default:
        return 'Sign-in failed. Please try again.';
    }
  }
}
