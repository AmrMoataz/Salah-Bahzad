# Dependency Injection in Flutter-Style Flutter

## Primary DI Stack

Use `get_it` for resolution and `injectable` for registration generation.

```dart
import 'package:get_it/get_it.dart';
import 'package:injectable/injectable.dart';
import 'configure.config.dart';

final getIt = GetIt.instance;

@InjectableInit()
Future<void> configureInjection([String? env]) async {
  await getIt.init(environment: env);
}
```

## Lifetime Strategy

| Registration | Use For |
|-------------|---------|
| singleton/lazySingleton | shared infrastructure and repositories |
| factory | Bloc instances, state handlers, short-lived mappers |

## Registration Example

```dart
@module
abstract class DataModule {
  @lazySingleton
  Dio dio() => Dio(BaseOptions(baseUrl: 'https://api.example.com'));
}

@LazySingleton(as: AuthRepository)
class AuthRepositoryImpl implements AuthRepository {
  AuthRepositoryImpl(this.service);
  final AuthService service;
}

@injectable
class LoginBloc extends Bloc<LoginEvent, LoginState> {
  LoginBloc(this.authRepository) : super(const LoginState());
  final AuthRepository authRepository;
}
```

## Environment Wiring

```dart
const dev = Environment('dev');
const staging = Environment('staging');
const prod = Environment('prod');

@dev
@LazySingleton(as: BaseUrlProvider)
class DevBaseUrlProvider implements BaseUrlProvider {
  @override
  String get value => 'https://dev.example.com';
}

@prod
@LazySingleton(as: BaseUrlProvider)
class ProdBaseUrlProvider implements BaseUrlProvider {
  @override
  String get value => 'https://api.example.com';
}
```

## Usage Boundaries

1. Resolve dependencies at screen/bootstrap boundaries.
2. Pass dependencies via constructors inside feature code.
3. Avoid resolving `getIt` inside deep widget subtrees repeatedly.
4. Keep UI declarative; DI is setup, not control flow.

## Testing Pattern

```dart
setUp(() async {
  await getIt.reset();
  getIt.registerFactory<LoginBloc>(() => LoginBloc(MockAuthRepository()));
});
```

## Anti-Patterns

1. Creating repositories/services directly inside widgets.
2. Mixing multiple DI systems as co-equal defaults.
3. Using service locator calls as business orchestration.
4. Registering feature state as global singleton by default.
