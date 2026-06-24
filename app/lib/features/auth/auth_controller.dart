import 'dart:async';
import 'dart:convert';

import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../app/providers.dart';
import '../../core/logging/logging.dart';
import '../../core/net/api_exception.dart';
import '../../core/storage/session.dart';
import '../../data/dtos/student_auth_response.dart';
import '../../data/dtos/student_summary.dart';
import 'auth_state.dart';
import 'identity_provider.dart';

/// The auth machine. Device-agnostic: it only ever drives `app-exchange`
/// (contract §A), persists the session in the keystore, and silently refreshes
/// before expiry (plan F4). A failed refresh / forced 401 → clean sign-out.
class AuthController extends Notifier<AuthState> {
  Timer? _refreshTimer;

  /// Safety margin — refresh this long before the access token actually expires.
  static const Duration _refreshLead = Duration(minutes: 1);

  AppLogger get _log => ref.read(loggerProvider).scoped('auth');

  @override
  AuthState build() {
    final client = ref.read(apiClientProvider);
    void onRevoked() => unawaited(_handleSessionExpired());
    client.onSessionRevoked.addListener(onRevoked);
    ref.onDispose(() {
      client.onSessionRevoked.removeListener(onRevoked);
      _refreshTimer?.cancel();
    });
    unawaited(_bootstrap());
    return const AuthUnknown();
  }

  // — Startup —

  Future<void> _bootstrap() async {
    final store = ref.read(sessionStoreProvider);
    final Session? session = await store.load();
    if (session == null || session.isRefreshExpired) {
      await store.clear();
      state = const AuthSignedOut();
      return;
    }
    final StudentSummary? student = await _loadCachedStudent();
    if (student == null) {
      // Tokens without a known identity (corrupt/upgraded blob) → re-auth.
      await store.clear();
      state = const AuthSignedOut();
      return;
    }
    if (session.isAccessExpired) {
      final Session? refreshed = await ref
          .read(tokenRefresherProvider)
          .refresh(session.refreshToken);
      if (refreshed == null) {
        await store.clear();
        state = const AuthSignedOut();
        return;
      }
      await store.save(refreshed);
      _scheduleRefresh(refreshed);
    } else {
      _scheduleRefresh(session);
    }
    state = AuthActive(student);
  }

  // — Sign in —

  Future<void> signInWithEmail({
    required String email,
    required String password,
    bool rememberMe = true,
  }) async {
    await _runSignIn(
      rememberMe: rememberMe,
      getToken: () => ref
          .read(identityProvider)
          .signInWithEmailPassword(email: email, password: password),
    );
  }

  Future<void> signInWithGoogle({bool rememberMe = true}) async {
    await _runSignIn(
      rememberMe: rememberMe,
      getToken: () => ref.read(identityProvider).signInWithGoogle(),
    );
  }

  Future<void> _runSignIn({
    required Future<String> Function() getToken,
    required bool rememberMe,
  }) async {
    state = const AuthSigningIn();
    try {
      final String firebaseIdToken = await getToken();
      final StudentAuthResponse res = await ref
          .read(authRepositoryProvider)
          .appExchange(firebaseIdToken);
      final store = ref.read(sessionStoreProvider);
      await store.save(res.toSession(), persist: rememberMe);
      await store.saveStudent(jsonEncode(res.student.toJson()));
      _scheduleRefresh(res.toSession());
      _log.info('Sign-in succeeded');
      state = AuthActive(res.student);
    } on IdentityException catch (e) {
      _log.debug(
        'Identity sign-in rejected',
        fields: <String, Object?>{'message': e.message},
      );
      state = AuthError(AuthErrorReason.invalidCredentials, e.message);
    } on ApiException catch (e) {
      _log.warning(
        'app-exchange failed',
        fields: <String, Object?>{'status': e.statusCode, 'reason': e.reason},
      );
      state = _mapApiError(e);
    } catch (error, stack) {
      _log.error('Unexpected sign-in failure', error: error, stackTrace: stack);
      state = const AuthError(
        AuthErrorReason.unknown,
        'Sign-in failed. Please try again.',
      );
    }
  }

  // — Sign out (FR-APP-AUTH-004) —

  Future<void> signOut() async {
    _refreshTimer?.cancel();
    try {
      await ref.read(identityProvider).signOut();
    } catch (error, stack) {
      // Identity sign-out failures must not block clearing the local session.
      _log.warning('Identity sign-out failed', error: error, stackTrace: stack);
    }
    await ref.read(sessionStoreProvider).clear();
    _log.info('Signed out');
    state = const AuthSignedOut();
  }

  /// Lets the Sign-in screen drop a stale error banner before retrying.
  void clearError() {
    if (state is AuthError) state = const AuthSignedOut();
  }

  // — Silent refresh —

  void _scheduleRefresh(Session session) {
    _refreshTimer?.cancel();
    final DateTime fireAt = session.accessTokenExpiresAt.toUtc().subtract(
      _refreshLead,
    );
    Duration delay = fireAt.difference(DateTime.now().toUtc());
    if (delay < const Duration(seconds: 5)) {
      delay = const Duration(seconds: 5);
    }
    _refreshTimer = Timer(delay, () => unawaited(_silentRefresh()));
  }

  Future<void> _silentRefresh() async {
    final store = ref.read(sessionStoreProvider);
    final Session? session = store.current;
    if (session == null) {
      await _handleSessionExpired();
      return;
    }
    final Session? next = await ref
        .read(tokenRefresherProvider)
        .refresh(session.refreshToken);
    if (next == null) {
      await _handleSessionExpired();
      return;
    }
    await store.save(next);
    _scheduleRefresh(next);
  }

  Future<void> _handleSessionExpired() async {
    _refreshTimer?.cancel();
    _log.info('Session expired; signing out');
    await ref.read(sessionStoreProvider).clear();
    state = const AuthSignedOut();
  }

  // — Helpers —

  Future<StudentSummary?> _loadCachedStudent() async {
    final String? raw = await ref.read(sessionStoreProvider).loadStudent();
    if (raw == null) return null;
    try {
      final dynamic m = jsonDecode(raw);
      if (m is Map) return StudentSummary.fromJson(m.cast<String, dynamic>());
    } catch (error, stack) {
      _log.warning(
        'Cached student blob unreadable; forcing re-auth',
        error: error,
        stackTrace: stack,
      );
    }
    return null;
  }

  AuthError _mapApiError(ApiException e) {
    if (e.isNetwork) {
      return const AuthError(
        AuthErrorReason.network,
        "You're offline. Check your connection and try again.",
      );
    }
    if (e.isServerError) {
      return const AuthError(
        AuthErrorReason.server,
        'Something went wrong on our end. Please try again.',
      );
    }
    switch (e.statusCode) {
      case 403:
        switch (e.reason) {
          case 'account_pending':
            return const AuthError(
              AuthErrorReason.accountPending,
              "Your account is awaiting approval. You'll be able to sign in "
              'once a teacher approves it.',
            );
          case 'account_rejected':
            return AuthError(
              AuthErrorReason.accountRejected,
              (e.detail != null && e.detail!.isNotEmpty)
                  ? e.detail!
                  : 'Your registration was not approved.',
            );
          case 'account_inactive':
            return const AuthError(
              AuthErrorReason.accountInactive,
              'Your account has been deactivated. Please contact your teacher.',
            );
          default:
            return AuthError(
              AuthErrorReason.unknown,
              e.detail ?? 'You are not allowed to sign in.',
            );
        }
      case 401:
        return const AuthError(
          AuthErrorReason.noStudent,
          'No student account is linked to this sign-in.',
        );
      case 429:
        return const AuthError(
          AuthErrorReason.rateLimited,
          'Too many attempts. Please wait a moment and try again.',
        );
      default:
        return AuthError(
          AuthErrorReason.unknown,
          e.detail ?? 'Sign-in failed. Please try again.',
        );
    }
  }
}
