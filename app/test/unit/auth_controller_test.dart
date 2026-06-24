import 'package:dio/dio.dart';
import 'package:flutter/foundation.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:secure_player/app/providers.dart';
import 'package:secure_player/core/net/api_client.dart';
import 'package:secure_player/core/net/api_exception.dart';
import 'package:secure_player/core/net/token_refresher.dart';
import 'package:secure_player/core/platform/app_platform.dart';
import 'package:secure_player/core/storage/session.dart';
import 'package:secure_player/core/storage/session_store.dart';
import 'package:secure_player/data/auth_repository.dart';
import 'package:secure_player/data/dtos/student_auth_response.dart';
import 'package:secure_player/data/dtos/student_profile.dart';
import 'package:secure_player/data/dtos/student_summary.dart';
import 'package:secure_player/features/auth/auth_state.dart';
import 'package:secure_player/features/auth/identity_provider.dart';

// ── Hand fakes (the dev/test path never touches Firebase — NFR-APP-REL-003) ──

class FakeIdentity implements IdentityProvider {
  FakeIdentity({this.error});

  final Object? error;
  bool signedOut = false;

  @override
  bool get googleSupported => true;

  @override
  Future<String> signInWithEmailPassword({
    required String email,
    required String password,
  }) async {
    if (error != null) throw error!;
    return 'firebase-id-token';
  }

  @override
  Future<String> signInWithGoogle() async {
    if (error != null) throw error!;
    return 'firebase-id-token';
  }

  @override
  Future<void> signOut() async => signedOut = true;
}

class FakeAuthRepository implements AuthRepository {
  FakeAuthRepository({this.response, this.error});

  final StudentAuthResponse? response;
  final Object? error;

  @override
  Future<StudentAuthResponse> appExchange(String firebaseIdToken) async {
    if (error != null) throw error!;
    return response!;
  }

  @override
  Future<StudentProfile> me() async => throw UnimplementedError();
}

class FakeSessionStore implements SessionStore {
  Session? _cached;
  String? _student;
  bool _persist = true;

  @override
  Session? get current => _cached;

  @override
  Future<Session?> load() async => _cached;

  @override
  Future<void> save(Session session, {bool? persist}) async {
    if (persist != null) _persist = persist;
    _cached = session;
  }

  @override
  Future<void> clear() async {
    _cached = null;
    _student = null;
    _persist = true;
  }

  @override
  Future<void> saveStudent(String json) async {
    if (_persist) _student = json;
  }

  @override
  Future<String?> loadStudent() async => _student;
}

class FakeApiClient implements ApiClient {
  @override
  final Dio dio = Dio();
  final ValueNotifier<int> _revoked = ValueNotifier<int>(0);

  @override
  ValueListenable<int> get onSessionRevoked => _revoked;

  @override
  void dispose() => _revoked.dispose();
}

class FakeTokenRefresher implements TokenRefresher {
  @override
  Future<Session?> refresh(String refreshToken) async => null;
}

// ── Helpers ──────────────────────────────────────────────────────────────────

StudentAuthResponse activeResponse() => StudentAuthResponse(
      accessToken: 'access',
      refreshToken: 'refresh',
      accessTokenExpiresAt: DateTime.now().toUtc().add(const Duration(minutes: 15)),
      refreshTokenExpiresAt: DateTime.now().toUtc().add(const Duration(days: 7)),
      student: const StudentSummary(
        id: 'stu-1',
        fullName: 'Layla Ahmed',
        status: 'Active',
      ),
    );

ProviderContainer makeContainer({
  IdentityProvider? identity,
  AuthRepository? repo,
  FakeSessionStore? store,
}) {
  final container = ProviderContainer(
    overrides: [
      identityProvider.overrideWith((ref) => identity ?? FakeIdentity()),
      authRepositoryProvider.overrideWith(
        (ref) => repo ?? FakeAuthRepository(response: activeResponse()),
      ),
      sessionStoreProvider.overrideWith((ref) => store ?? FakeSessionStore()),
      apiClientProvider.overrideWith((ref) => FakeApiClient()),
      tokenRefresherProvider.overrideWith((ref) => FakeTokenRefresher()),
      appPlatformProvider
          .overrideWith((ref) => AppPlatform(target: AppTarget.android)),
    ],
  );
  addTearDown(container.dispose);
  return container;
}

/// Flushes the build-time bootstrap microtask so the controller settles to its
/// initial signed-out state.
Future<void> settle() => Future<void>.delayed(const Duration(milliseconds: 10));

void main() {
  test('bootstrap with no stored session → AuthSignedOut', () async {
    final container = makeContainer();
    container.read(authControllerProvider.notifier);
    await settle();
    expect(container.read(authControllerProvider), isA<AuthSignedOut>());
  });

  test('email sign-in (happy path) → AuthActive with the student', () async {
    final container = makeContainer(
      repo: FakeAuthRepository(response: activeResponse()),
    );
    final notifier = container.read(authControllerProvider.notifier);
    await settle();
    await notifier.signInWithEmail(email: 'a@b.c', password: 'pw');

    final AuthState state = container.read(authControllerProvider);
    expect(state, isA<AuthActive>());
    expect((state as AuthActive).student.fullName, 'Layla Ahmed');
  });

  test('Google sign-in (happy path) → AuthActive', () async {
    final container = makeContainer();
    final notifier = container.read(authControllerProvider.notifier);
    await settle();
    await notifier.signInWithGoogle();
    expect(container.read(authControllerProvider), isA<AuthActive>());
  });

  group('status-gate 403 reasons map to the right error state (contract §A.2)',
      () {
    final Map<String, AuthErrorReason> cases = <String, AuthErrorReason>{
      'account_pending': AuthErrorReason.accountPending,
      'account_rejected': AuthErrorReason.accountRejected,
      'account_inactive': AuthErrorReason.accountInactive,
    };

    cases.forEach((String reason, AuthErrorReason expected) {
      test('$reason → $expected', () async {
        final container = makeContainer(
          repo: FakeAuthRepository(
            error: ApiException(statusCode: 403, reason: reason, detail: 'why'),
          ),
        );
        final notifier = container.read(authControllerProvider.notifier);
        await settle();
        await notifier.signInWithEmail(email: 'a@b.c', password: 'pw');

        final AuthState state = container.read(authControllerProvider);
        expect(state, isA<AuthError>());
        expect((state as AuthError).reason, expected);
      });
    });
  });

  test('401 → noStudent', () async {
    final container = makeContainer(
      repo: FakeAuthRepository(error: ApiException(statusCode: 401)),
    );
    final notifier = container.read(authControllerProvider.notifier);
    await settle();
    await notifier.signInWithEmail(email: 'a@b.c', password: 'pw');
    expect(
      (container.read(authControllerProvider) as AuthError).reason,
      AuthErrorReason.noStudent,
    );
  });

  test('429 → rateLimited', () async {
    final container = makeContainer(
      repo: FakeAuthRepository(error: ApiException(statusCode: 429)),
    );
    final notifier = container.read(authControllerProvider.notifier);
    await settle();
    await notifier.signInWithEmail(email: 'a@b.c', password: 'pw');
    expect(
      (container.read(authControllerProvider) as AuthError).reason,
      AuthErrorReason.rateLimited,
    );
  });

  test('network failure → network', () async {
    final container = makeContainer(
      repo: FakeAuthRepository(error: ApiException(statusCode: null, isNetwork: true)),
    );
    final notifier = container.read(authControllerProvider.notifier);
    await settle();
    await notifier.signInWithEmail(email: 'a@b.c', password: 'pw');
    expect(
      (container.read(authControllerProvider) as AuthError).reason,
      AuthErrorReason.network,
    );
  });

  test('wrong credentials (IdentityException) → invalidCredentials', () async {
    final container = makeContainer(
      identity: FakeIdentity(
        error: IdentityException('wrong-password', 'Wrong email or password.'),
      ),
    );
    final notifier = container.read(authControllerProvider.notifier);
    await settle();
    await notifier.signInWithEmail(email: 'a@b.c', password: 'bad');

    final AuthState state = container.read(authControllerProvider);
    expect(state, isA<AuthError>());
    expect((state as AuthError).reason, AuthErrorReason.invalidCredentials);
    expect(state.message, 'Wrong email or password.');
  });

  test('signOut → AuthSignedOut and clears identity', () async {
    final FakeIdentity identity = FakeIdentity();
    final container = makeContainer(identity: identity);
    final notifier = container.read(authControllerProvider.notifier);
    await settle();
    await notifier.signInWithEmail(email: 'a@b.c', password: 'pw');
    expect(container.read(authControllerProvider), isA<AuthActive>());

    await notifier.signOut();
    expect(container.read(authControllerProvider), isA<AuthSignedOut>());
    expect(identity.signedOut, isTrue);
  });
}
