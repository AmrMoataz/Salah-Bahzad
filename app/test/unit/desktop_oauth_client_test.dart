import 'dart:math';

import 'package:flutter_test/flutter_test.dart';
import 'package:secure_player/features/auth/google/desktop_oauth_client.dart';
import 'package:secure_player/features/auth/identity_provider.dart';

/// Pure-function coverage for the desktop OAuth helpers — no browser, sockets,
/// or network (`NFR-APP-REL-003`).
void main() {
  group('Pkce', () {
    test('S256 challenge matches the RFC 7636 reference vector', () {
      // From RFC 7636 Appendix B.
      const String verifier = 'dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk';
      expect(
        Pkce.challengeFor(verifier),
        'E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM',
      );
    });

    test('generate(): challenge == base64url(sha256(verifier)), url-safe', () {
      final Pkce pkce = Pkce.generate(random: Random(7));
      expect(pkce.challenge, Pkce.challengeFor(pkce.verifier));
      // base64url, no padding — never contains +, /, or =.
      expect(pkce.verifier, isNot(contains('=')));
      expect(pkce.challenge, isNot(matches(RegExp(r'[+/=]'))));
      expect(pkce.verifier.length, greaterThanOrEqualTo(43));
    });
  });

  group('buildAuthUrl', () {
    final Uri uri = buildAuthUrl(
      authority: 'https://accounts.google.com/o/oauth2/v2/auth',
      clientId: 'client-123.apps.googleusercontent.com',
      redirectUri: 'http://127.0.0.1:54321',
      scopes: <String>['openid', 'email', 'profile'],
      codeChallenge: 'CHALLENGE',
      state: 'STATE',
    );

    test('targets the Google auth endpoint with all installed-app params', () {
      expect(uri.origin, 'https://accounts.google.com');
      expect(uri.path, '/o/oauth2/v2/auth');
      final Map<String, String> q = uri.queryParameters;
      expect(q['client_id'], 'client-123.apps.googleusercontent.com');
      expect(q['redirect_uri'], 'http://127.0.0.1:54321');
      expect(q['response_type'], 'code');
      expect(q['scope'], 'openid email profile');
      expect(q['code_challenge'], 'CHALLENGE');
      expect(q['code_challenge_method'], 'S256');
      expect(q['state'], 'STATE');
    });
  });

  group('parseRedirect', () {
    test('valid redirect → returns the code', () {
      final Uri uri = Uri.parse('http://127.0.0.1:8080/?code=abc123&state=S');
      expect(parseRedirect(uri, expectedState: 'S'), 'abc123');
    });

    test('state mismatch → google_state_mismatch', () {
      final Uri uri = Uri.parse('http://127.0.0.1:8080/?code=abc&state=WRONG');
      expect(
        () => parseRedirect(uri, expectedState: 'S'),
        throwsA(
          isA<IdentityException>().having(
            (IdentityException e) => e.code,
            'code',
            'google_state_mismatch',
          ),
        ),
      );
    });

    test('error=access_denied → google_cancelled', () {
      final Uri uri = Uri.parse(
        'http://127.0.0.1:8080/?error=access_denied&state=S',
      );
      expect(
        () => parseRedirect(uri, expectedState: 'S'),
        throwsA(
          isA<IdentityException>().having(
            (IdentityException e) => e.code,
            'code',
            'google_cancelled',
          ),
        ),
      );
    });

    test('missing code → google_no_code', () {
      final Uri uri = Uri.parse('http://127.0.0.1:8080/?state=S');
      expect(
        () => parseRedirect(uri, expectedState: 'S'),
        throwsA(
          isA<IdentityException>().having(
            (IdentityException e) => e.code,
            'code',
            'google_no_code',
          ),
        ),
      );
    });
  });
}
