# Layered Architecture in Flutter-Style Flutter

## Layer Model

Flutter uses a pragmatic layered model, not a strict domain/use-case-heavy setup:

```
ui -> presentation -> data -> external systems
```

- `ui`: declarative widgets and feature screens.
- `presentation`: Bloc events/states and feature flow.
- `data`: repositories, services, models, mappers, API/storage integration.

## Typical Request Flow

1. User action in `ui` dispatches a Bloc event.
2. Bloc in `presentation` calls repository/service abstractions.
3. Repository/service in `data` executes Dio/Firebase/storage operations.
4. Bloc emits new immutable state.
5. UI rebuilds declaratively from state.

## Presentation Example

```dart
class LoginBloc extends Bloc<LoginEvent, LoginState> {
  LoginBloc(this.repository) : super(const LoginState()) {
    on<LoginRequested>(_onLoginRequested);
  }

  final AuthRepository repository;

  Future<void> _onLoginRequested(
    LoginRequested event,
    Emitter<LoginState> emit,
  ) async {
    emit(state.copyWith(status: LoginStatus.loading));
    final result = await repository.login(event.email, event.password);
    emit(
      result.when(
        success: (user) => state.copyWith(status: LoginStatus.success, user: user),
        error: (message) => state.copyWith(status: LoginStatus.failure, message: message),
      ),
    );
  }
}
```

## Data Boundary Example

```dart
abstract class AuthRepository {
  Future<DataResult<UserModel>> login(String email, String password);
}

class AuthRepositoryImpl implements AuthRepository {
  AuthRepositoryImpl(this.service);
  final AuthService service;

  @override
  Future<DataResult<UserModel>> login(String email, String password) {
    return service.login(email, password);
  }
}
```

## Declarative UI Rule

```dart
BlocBuilder<LoginBloc, LoginState>(
  builder: (context, state) {
    if (state.status == LoginStatus.loading) {
      return const CircularProgressIndicator();
    }
    return LoginForm(
      onSubmit: (email, password) {
        context.read<LoginBloc>().add(LoginRequested(email, password));
      },
    );
  },
);
```

## Architecture Constraints

1. Do not put networking/storage logic in widgets.
2. Do not call Dio/Firebase directly from Bloc.
3. Do not mutate state objects in place.
4. Keep feature behavior in Bloc and side-effect boundaries in data/services.
5. Prefer technology parity with Flutter stack over introducing parallel architecture defaults.
