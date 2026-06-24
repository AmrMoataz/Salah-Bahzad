import 'package:firebase_auth/firebase_auth.dart';

import 'google/google_credential_source.dart';

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
  /// Whether "Continue with Google" is offered here. True on mobile/macOS
  /// (the `google_sign_in` plugin), and on Windows when the desktop OAuth
  /// client is configured (system-browser loopback flow).
  bool get googleSupported;

  Future<String> signInWithEmailPassword({
    required String email,
    required String password,
  });

  Future<String> signInWithGoogle();

  Future<void> signOut();
}

/// Real implementation over `firebase_auth`. Google sign-in is uniform across
/// platforms: a [GoogleCredentialSource] yields a Firebase credential (from the
/// `google_sign_in` plugin on mobile/macOS, or the desktop OAuth loopback on
/// Windows), and this provider trades it for a Firebase ID token via the **one**
/// `signInWithCredential` path.
class FirebaseIdentityProvider implements IdentityProvider {
  FirebaseIdentityProvider({
    required this.googleSupported,
    required this.googleSource,
    FirebaseAuth? auth,
  }) : _auth = auth ?? FirebaseAuth.instance;

  /// True on mobile/macOS, and on Windows when the desktop OAuth client is set.
  static bool computeGoogleSupported({
    required bool isWindows,
    required bool hasDesktopGoogleOAuth,
  }) => !isWindows || hasDesktopGoogleOAuth;

  @override
  final bool googleSupported;
  final GoogleCredentialSource googleSource;
  final FirebaseAuth _auth;

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
      final AuthCredential credential = await googleSource.getCredential();
      final UserCredential cred = await _auth.signInWithCredential(credential);
      return _idTokenOf(cred);
    } on FirebaseAuthException catch (e) {
      throw IdentityException(e.code, _friendly(e.code));
    }
    // IdentityExceptions from the source (cancel/timeout/unconfigured/no_token)
    // propagate unchanged for the controller to render inline.
  }

  @override
  Future<void> signOut() async {
    await _auth.signOut();
    if (googleSupported) {
      try {
        await googleSource.signOut();
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
