# Mocking in Flutter-Style Flutter Tests

## Preferred Stack

Use `mockito` or `mocktail` with `bloc_test` and `flutter_test`. Mock repositories/services and keep bloc/widget tests focused on behavior.

## Basic Mock Example

```dart
class MockAuthRepository extends Mock implements AuthRepository {}
```

```dart
when(() => mockAuthRepository.login('admin@flutter.com', 'secret'))
    .thenAnswer((_) async => Success(UserModel(id: '1')));
```

## Verify Interactions

```dart
verify(() => mockAuthRepository.login('admin@flutter.com', 'secret')).called(1);
verifyNever(() => mockAuthRepository.logout());
```

## Bloc Test Pattern

```dart
blocTest<LoginBloc, LoginState>(
  'emits loading then success',
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

## Dio Boundary Mocking

Mock repository/service boundaries first. Mocking Dio adapter directly is optional and should be used only for dedicated networking unit tests.

## Rules

1. Mock behavior that affects test outcome.
2. Avoid over-verifying implementation details.
3. Keep stubs explicit and deterministic.
4. Avoid coupling tests to non-public internals.
