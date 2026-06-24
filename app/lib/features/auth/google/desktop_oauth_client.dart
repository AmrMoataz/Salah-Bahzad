import 'dart:async';
import 'dart:convert';
import 'dart:io';
import 'dart:math';

import 'package:crypto/crypto.dart';
import 'package:dio/dio.dart';
import 'package:url_launcher/url_launcher.dart';

import '../identity_provider.dart';

/// The Google OAuth tokens obtained from the desktop loopback flow. These are
/// the **Google** `idToken`/`accessToken` that Firebase then consumes via
/// `GoogleAuthProvider.credential` — never the app's own session tokens.
class OAuthTokens {
  const OAuthTokens({required this.idToken, this.accessToken});

  final String idToken;
  final String? accessToken;

  /// Never leak the tokens into logs (`NFR-APP-SEC-003`).
  @override
  String toString() => 'OAuthTokens(idToken: <redacted>)';
}

/// PKCE (RFC 7636) `code_verifier` + S256 `code_challenge`. Pure & unit-tested:
/// `challenge == base64url(sha256(verifier))` with no padding.
class Pkce {
  const Pkce({required this.verifier, required this.challenge});

  final String verifier;
  final String challenge;

  /// Generate a high-entropy verifier (43–128 unreserved chars) and its S256
  /// challenge. [random] is injectable so tests are deterministic.
  factory Pkce.generate({Random? random}) {
    final Random rng = random ?? Random.secure();
    final String verifier = _randomUrlToken(32, rng);
    final String challenge = challengeFor(verifier);
    return Pkce(verifier: verifier, challenge: challenge);
  }

  /// The S256 transform — exposed so it can be asserted directly.
  static String challengeFor(String verifier) =>
      _b64Url(sha256.convert(ascii.encode(verifier)).bytes);
}

/// A random URL-safe state/verifier token of [bytes] entropy (base64url, no pad).
String _randomUrlToken(int bytes, Random rng) {
  final List<int> raw = List<int>.generate(
    bytes,
    (_) => rng.nextInt(256),
    growable: false,
  );
  return _b64Url(raw);
}

String _b64Url(List<int> bytes) => base64Url.encode(bytes).replaceAll('=', '');

/// Build the Google authorization URL for an installed/desktop app.
Uri buildAuthUrl({
  required String authority,
  required String clientId,
  required String redirectUri,
  required List<String> scopes,
  required String codeChallenge,
  required String state,
}) {
  return Uri.parse(authority).replace(
    queryParameters: <String, String>{
      'client_id': clientId,
      'redirect_uri': redirectUri,
      'response_type': 'code',
      'scope': scopes.join(' '),
      'code_challenge': codeChallenge,
      'code_challenge_method': 'S256',
      'state': state,
    },
  );
}

/// Validate the loopback redirect and extract the authorization `code`.
///
/// - `error=…` (e.g. `access_denied`) → user cancelled → `google_cancelled`.
/// - `state` mismatch → `google_state_mismatch` (CSRF guard).
/// - missing `code` → `google_no_code`.
String parseRedirect(Uri uri, {required String expectedState}) {
  final String? error = uri.queryParameters['error'];
  if (error != null) {
    throw IdentityException(
      'google_cancelled',
      'Google sign-in was cancelled.',
    );
  }
  if (uri.queryParameters['state'] != expectedState) {
    throw IdentityException('google_state_mismatch', 'Google sign-in failed.');
  }
  final String? code = uri.queryParameters['code'];
  if (code == null || code.isEmpty) {
    throw IdentityException('google_no_code', 'Google sign-in failed.');
  }
  return code;
}

/// Runs the desktop OAuth handshake and returns Google tokens. Behind this seam
/// so `flutter test` injects a fake — no browser, sockets, or network in tests
/// (`NFR-APP-REL-003`).
abstract class DesktopOAuthClient {
  Future<OAuthTokens> authorize({
    required String authority,
    required String tokenEndpoint,
    required String clientId,
    required String clientSecret,
    required List<String> scopes,
  });
}

/// Real impl: system browser + `127.0.0.1` loopback (Google's guidance for
/// installed apps; loopback is Windows-Firewall-exempt → no prompt). PKCE +
/// `state` protect the exchange; TLS is **never** disabled on the token POST
/// (`NFR-APP-SEC-002`); the `code`/tokens are never logged (`NFR-APP-SEC-003`).
class SystemBrowserOAuthClient implements DesktopOAuthClient {
  SystemBrowserOAuthClient({
    Dio? httpClient,
    this.timeout = const Duration(minutes: 3),
  }) : _dio = httpClient ?? Dio();

  /// A **bare** Dio (no app interceptor/auth) for the unauthenticated token POST.
  final Dio _dio;

  /// How long to wait for the user to finish in the browser before giving up.
  final Duration timeout;

  @override
  Future<OAuthTokens> authorize({
    required String authority,
    required String tokenEndpoint,
    required String clientId,
    required String clientSecret,
    required List<String> scopes,
  }) async {
    final Pkce pkce = Pkce.generate();
    final String state = _randomUrlToken(16, Random.secure());

    final HttpServer server = await HttpServer.bind(
      InternetAddress.loopbackIPv4,
      0,
    );
    try {
      final String redirectUri = 'http://127.0.0.1:${server.port}';
      final Uri authUri = buildAuthUrl(
        authority: authority,
        clientId: clientId,
        redirectUri: redirectUri,
        scopes: scopes,
        codeChallenge: pkce.challenge,
        state: state,
      );

      final bool launched = await launchUrl(
        authUri,
        mode: LaunchMode.externalApplication,
      );
      if (!launched) {
        throw IdentityException(
          'google_launch_failed',
          "Couldn't open your browser for Google sign-in.",
        );
      }

      final String code = await _awaitRedirect(server, state);
      return _exchangeCode(
        tokenEndpoint: tokenEndpoint,
        code: code,
        clientId: clientId,
        clientSecret: clientSecret,
        redirectUri: redirectUri,
        codeVerifier: pkce.verifier,
      );
    } finally {
      await server.close(force: true);
    }
  }

  /// Await the single browser redirect, answer it with a friendly page, and
  /// return the authorization `code`. Times out if the user never finishes.
  Future<String> _awaitRedirect(HttpServer server, String state) async {
    final Completer<String> completer = Completer<String>();

    final StreamSubscription<HttpRequest> sub = server.listen((
      HttpRequest request,
    ) async {
      try {
        final String code = parseRedirect(request.uri, expectedState: state);
        await _respond(request, _successHtml);
        if (!completer.isCompleted) completer.complete(code);
      } on IdentityException catch (e) {
        await _respond(request, _failureHtml);
        if (!completer.isCompleted) completer.completeError(e);
      }
    });

    try {
      return await completer.future.timeout(
        timeout,
        onTimeout: () => throw IdentityException(
          'google_timeout',
          "Google sign-in didn't finish in time. Please try again.",
        ),
      );
    } finally {
      await sub.cancel();
    }
  }

  Future<void> _respond(HttpRequest request, String body) async {
    request.response
      ..statusCode = HttpStatus.ok
      ..headers.contentType = ContentType.html;
    request.response.write(body);
    await request.response.close();
  }

  Future<OAuthTokens> _exchangeCode({
    required String tokenEndpoint,
    required String code,
    required String clientId,
    required String clientSecret,
    required String redirectUri,
    required String codeVerifier,
  }) async {
    try {
      final Response<dynamic> res = await _dio.post<dynamic>(
        tokenEndpoint,
        data: <String, String>{
          'code': code,
          'client_id': clientId,
          'client_secret': clientSecret,
          'redirect_uri': redirectUri,
          'grant_type': 'authorization_code',
          'code_verifier': codeVerifier,
        },
        options: Options(contentType: Headers.formUrlEncodedContentType),
      );
      final dynamic data = res.data;
      final String? idToken = data is Map ? data['id_token'] as String? : null;
      if (idToken == null) {
        throw IdentityException('google_no_token', 'Google sign-in failed.');
      }
      final String? accessToken = data is Map
          ? data['access_token'] as String?
          : null;
      return OAuthTokens(idToken: idToken, accessToken: accessToken);
    } on DioException {
      // Body is intentionally not surfaced — it can echo the code/secret.
      throw IdentityException(
        'google_exchange_failed',
        'Google sign-in failed. Please try again.',
      );
    }
  }

  static const String _successHtml =
      '<!doctype html><meta charset="utf-8"><title>Salah Bahzad</title>'
      '<body style="font-family:sans-serif;text-align:center;padding-top:80px">'
      '<h2>You can close this tab</h2>'
      '<p>Return to Salah Bahzad to continue.</p></body>';

  static const String _failureHtml =
      '<!doctype html><meta charset="utf-8"><title>Salah Bahzad</title>'
      '<body style="font-family:sans-serif;text-align:center;padding-top:80px">'
      '<h2>Sign-in was cancelled</h2>'
      '<p>You can close this tab and try again in Salah Bahzad.</p></body>';
}
