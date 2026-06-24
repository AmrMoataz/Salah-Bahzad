import 'package:flutter_test/flutter_test.dart';
import 'package:secure_player/features/auth/identity_provider.dart';

/// The "offer Google?" decision (drives the Sign-in button + divider). Pure, so
/// it asserts the Windows-with-config case without touching Firebase.
void main() {
  test('mobile/macOS → always supported', () {
    expect(
      FirebaseIdentityProvider.computeGoogleSupported(
        isWindows: false,
        hasDesktopGoogleOAuth: false,
      ),
      isTrue,
    );
  });

  test('Windows without config → hidden', () {
    expect(
      FirebaseIdentityProvider.computeGoogleSupported(
        isWindows: true,
        hasDesktopGoogleOAuth: false,
      ),
      isFalse,
    );
  });

  test('Windows with desktop OAuth config → now shown', () {
    expect(
      FirebaseIdentityProvider.computeGoogleSupported(
        isWindows: true,
        hasDesktopGoogleOAuth: true,
      ),
      isTrue,
    );
  });
}
