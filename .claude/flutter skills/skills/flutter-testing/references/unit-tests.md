# Unit Testing in Flutter-Style Flutter

## Test Layout

Mirror Flutter layer structure in `test/`:

```
lib/
  data/
    auth/
      repo/
        auth_repository_impl.dart
  presentation/
    login/
      login_bloc.dart
test/
  data/
    auth/
      repo/
        auth_repository_impl_test.dart
  presentation/
    login/
      login_bloc_test.dart
```

## Core Practices

1. Keep tests deterministic and isolated.
2. Mock data boundaries and assert observable behavior.
3. Prefer behavior-driven test names.
4. Avoid testing private implementation details.

## Repository Unit Test Example

```dart
class MockAuthService extends Mock implements AuthService {}

void main() {
  late MockAuthService mockAuthService;
  late AuthRepositoryImpl repository;

  setUp(() {
    mockAuthService = MockAuthService();
    repository = AuthRepositoryImpl(mockAuthService);
  });

  test('returns success when service returns success', () async {
    when(() => mockAuthService.login('admin@flutter.com', 'secret'))
        .thenAnswer((_) async => Success(UserModel(id: '1')));

    final result = await repository.login('admin@flutter.com', 'secret');

    expect(result, isA<Success<UserModel>>());
    verify(() => mockAuthService.login('admin@flutter.com', 'secret')).called(1);
  });
}
```

## Bloc Unit Test Example

```dart
blocTest<LoginBloc, LoginState>(
  'emits loading then success on valid login',
  setUp: () {
    when(() => mockAuthRepository.login(any(), any()))
        .thenAnswer((_) async => Success(UserModel(id: '1')));
  },
  build: () => LoginBloc(mockAuthRepository),
  act: (bloc) => bloc.add(const LoginRequested('admin@flutter.com', 'secret')),
  expect: () => [
    const LoginState(status: LoginStatus.loading),
    const LoginState(status: LoginStatus.success),
  ],
);
```

## Useful Commands

```bash
flutter test
flutter test --coverage
flutter test test/presentation/login/login_bloc_test.dart
```
