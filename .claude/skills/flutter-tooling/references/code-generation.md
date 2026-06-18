# Code Generation with build_runner

## build_runner Commands

### Installation

Add `build_runner` as a dev dependency:

```bash
flutter pub add --dev build_runner
```

### Core Commands

```bash
# Run a one-time build (generates all files)
dart run build_runner build

# Delete conflicting outputs and rebuild (recommended)
dart run build_runner build --delete-conflicting-outputs

# Watch for file changes and rebuild automatically
dart run build_runner watch

# Watch with delete conflicting outputs
dart run build_runner watch --delete-conflicting-outputs

# Clean all generated files
dart run build_runner clean

# Build with verbose output for debugging
dart run build_runner build --verbose

# Build a specific directory only
dart run build_runner build --build-filter="lib/models/*"
```

> Note: Always use `dart run build_runner` instead of the deprecated `flutter pub run build_runner`.

---

## freezed -- Data Classes and Unions

### Setup

```bash
flutter pub add freezed_annotation
flutter pub add --dev freezed
flutter pub add --dev build_runner

# If you also need JSON serialization:
flutter pub add json_annotation
flutter pub add --dev json_serializable
```

### Basic Data Class

```dart
// file: lib/models/user.dart

import 'package:freezed_annotation/freezed_annotation.dart';

part 'user.freezed.dart';
part 'user.g.dart'; // only if using JSON serialization

@freezed
abstract class User with _$User {
  const factory User({
    required String id,
    required String name,
    required String email,
    @Default(false) bool isActive,
    DateTime? lastLogin,
  }) = _User;

  factory User.fromJson(Map<String, dynamic> json) => _$UserFromJson(json);
}
```

Usage:

```dart
final user = User(id: '1', name: 'Alice', email: 'alice@example.com');

// copyWith -- creates a new instance with modified fields
final updatedUser = user.copyWith(name: 'Alice Smith', isActive: true);

// Equality comparison (deep value equality)
print(user == User(id: '1', name: 'Alice', email: 'alice@example.com')); // true

// toString
print(user); // User(id: 1, name: Alice, email: alice@example.com, isActive: false, lastLogin: null)

// JSON serialization
final json = user.toJson();
final fromJson = User.fromJson(json);
```

### Union Types (Sealed Classes)

```dart
// file: lib/models/auth_state.dart

import 'package:freezed_annotation/freezed_annotation.dart';

part 'auth_state.freezed.dart';

@freezed
sealed class AuthState with _$AuthState {
  const factory AuthState.initial() = AuthInitial;
  const factory AuthState.loading() = AuthLoading;
  const factory AuthState.authenticated({required String userId, required String token}) = AuthAuthenticated;
  const factory AuthState.unauthenticated({String? errorMessage}) = AuthUnauthenticated;
}
```

Pattern matching usage:

```dart
String describeState(AuthState state) {
  return switch (state) {
    AuthInitial() => 'Not started',
    AuthLoading() => 'Loading...',
    AuthAuthenticated(:final userId) => 'Logged in as $userId',
    AuthUnauthenticated(:final errorMessage) => errorMessage ?? 'Logged out',
  };
}
```

### Adding Custom Methods

```dart
@freezed
abstract class Temperature with _$Temperature {
  const Temperature._(); // Required for custom methods

  const factory Temperature.celsius(double value) = _Celsius;
  const factory Temperature.fahrenheit(double value) = _Fahrenheit;

  double get toCelsius => switch (this) {
    _Celsius(:final value) => value,
    _Fahrenheit(:final value) => (value - 32) * 5 / 9,
  };

  double get toFahrenheit => switch (this) {
    _Celsius(:final value) => value * 9 / 5 + 32,
    _Fahrenheit(:final value) => value,
  };
}
```

### Generic Freezed Classes

```dart
@freezed
abstract class ApiResponse<T> with _$ApiResponse<T> {
  const factory ApiResponse.success(T data) = ApiSuccess<T>;
  const factory ApiResponse.error(String message, {int? statusCode}) = ApiError<T>;
  const factory ApiResponse.loading() = ApiLoading<T>;
}
```

---

## json_serializable -- JSON Serialization

### Setup

```bash
flutter pub add json_annotation
flutter pub add --dev json_serializable
flutter pub add --dev build_runner
```

### Basic Usage

```dart
// file: lib/models/product.dart

import 'package:json_annotation/json_annotation.dart';

part 'product.g.dart';

@JsonSerializable()
class Product {
  final String id;
  final String name;
  final double price;

  @JsonKey(name: 'is_available')
  final bool isAvailable;

  @JsonKey(name: 'created_at')
  final DateTime createdAt;

  @JsonKey(includeIfNull: false)
  final String? description;

  const Product({
    required this.id,
    required this.name,
    required this.price,
    required this.isAvailable,
    required this.createdAt,
    this.description,
  });

  factory Product.fromJson(Map<String, dynamic> json) => _$ProductFromJson(json);
  Map<String, dynamic> toJson() => _$ProductToJson(this);
}
```

### @JsonKey Annotations

```dart
@JsonSerializable()
class Order {
  // Rename the JSON key
  @JsonKey(name: 'order_id')
  final String orderId;

  // Provide a default value
  @JsonKey(defaultValue: 'pending')
  final String status;

  // Exclude from serialization
  @JsonKey(includeToJson: false)
  final String internalNote;

  // Exclude from deserialization
  @JsonKey(includeFromJson: false)
  final String computedField;

  // Custom converter
  @JsonKey(fromJson: _dateFromEpoch, toJson: _dateToEpoch)
  final DateTime timestamp;

  // Ignore null values in JSON output
  @JsonKey(includeIfNull: false)
  final String? optionalField;

  // Disallow null in JSON input
  @JsonKey(disallowNullValue: true)
  final String requiredField;

  const Order({
    required this.orderId,
    required this.status,
    required this.internalNote,
    this.computedField = '',
    required this.timestamp,
    this.optionalField,
    required this.requiredField,
  });

  factory Order.fromJson(Map<String, dynamic> json) => _$OrderFromJson(json);
  Map<String, dynamic> toJson() => _$OrderToJson(this);

  static DateTime _dateFromEpoch(int epoch) =>
      DateTime.fromMillisecondsSinceEpoch(epoch);
  static int _dateToEpoch(DateTime date) => date.millisecondsSinceEpoch;
}
```

### Nested Objects and Lists

```dart
@JsonSerializable(explicitToJson: true)
class Invoice {
  final String id;
  final Customer customer;
  final List<LineItem> items;

  const Invoice({required this.id, required this.customer, required this.items});

  factory Invoice.fromJson(Map<String, dynamic> json) => _$InvoiceFromJson(json);
  Map<String, dynamic> toJson() => _$InvoiceToJson(this);
}
```

> Use `explicitToJson: true` when you have nested objects so their `toJson` methods are called.

### Custom JsonConverter

```dart
class ColorConverter implements JsonConverter<Color, int> {
  const ColorConverter();

  @override
  Color fromJson(int json) => Color(json);

  @override
  int toJson(Color object) => object.value;
}

@JsonSerializable()
class Theme {
  @ColorConverter()
  final Color primaryColor;

  @ColorConverter()
  final Color accentColor;

  const Theme({required this.primaryColor, required this.accentColor});

  factory Theme.fromJson(Map<String, dynamic> json) => _$ThemeFromJson(json);
  Map<String, dynamic> toJson() => _$ThemeToJson(this);
}
```

### Enum Serialization

```dart
@JsonEnum(valueField: 'code')
enum Priority {
  low('L'),
  medium('M'),
  high('H');

  final String code;
  const Priority(this.code);
}

@JsonSerializable()
class Task {
  final String title;
  final Priority priority;

  const Task({required this.title, required this.priority});

  factory Task.fromJson(Map<String, dynamic> json) => _$TaskFromJson(json);
  Map<String, dynamic> toJson() => _$TaskToJson(this);
}
// JSON: {"title": "Fix bug", "priority": "H"}
```

---

## riverpod_generator -- Riverpod Code Generation

### Setup

```bash
flutter pub add riverpod_annotation
flutter pub add flutter_riverpod
flutter pub add --dev riverpod_generator
flutter pub add --dev build_runner
# Optional: for riverpod_lint rules
flutter pub add --dev custom_lint
flutter pub add --dev riverpod_lint
```

### Simple Provider

```dart
// file: lib/providers/counter.dart

import 'package:riverpod_annotation/riverpod_annotation.dart';

part 'counter.g.dart';

// A simple synchronous provider
@riverpod
int counter(Ref ref) {
  return 0;
}

// A provider that depends on another provider
@riverpod
String counterLabel(Ref ref) {
  final count = ref.watch(counterProvider);
  return 'Count: $count';
}
```

### Async Provider

```dart
import 'package:riverpod_annotation/riverpod_annotation.dart';

part 'user_repository.g.dart';

@riverpod
Future<List<User>> users(Ref ref) async {
  final dio = ref.watch(dioProvider);
  final response = await dio.get('/users');
  return (response.data as List).map((e) => User.fromJson(e)).toList();
}

// Stream provider
@riverpod
Stream<int> tick(Ref ref) {
  return Stream.periodic(const Duration(seconds: 1), (i) => i);
}
```

### Notifier (Stateful Provider)

```dart
import 'package:riverpod_annotation/riverpod_annotation.dart';

part 'todo_list.g.dart';

@riverpod
class TodoList extends _$TodoList {
  @override
  List<Todo> build() {
    // Return initial state
    return [];
  }

  void addTodo(Todo todo) {
    state = [...state, todo];
  }

  void removeTodo(String id) {
    state = state.where((todo) => todo.id != id).toList();
  }

  void toggleTodo(String id) {
    state = [
      for (final todo in state)
        if (todo.id == id) todo.copyWith(isCompleted: !todo.isCompleted) else todo,
    ];
  }
}
```

### Async Notifier

```dart
import 'package:riverpod_annotation/riverpod_annotation.dart';

part 'auth_controller.g.dart';

@riverpod
class AuthController extends _$AuthController {
  @override
  Future<User?> build() async {
    // Check for stored credentials on initialization
    final token = await ref.watch(secureStorageProvider).read('token');
    if (token != null) {
      return ref.watch(userRepositoryProvider).getCurrentUser(token);
    }
    return null;
  }

  Future<void> signIn(String email, String password) async {
    state = const AsyncLoading();
    state = await AsyncValue.guard(() async {
      final repo = ref.read(userRepositoryProvider);
      return repo.signIn(email, password);
    });
  }

  Future<void> signOut() async {
    await ref.read(secureStorageProvider).delete('token');
    state = const AsyncData(null);
  }
}
```

### Family Providers (Parameterized)

```dart
@riverpod
Future<User> userById(Ref ref, String userId) async {
  final repo = ref.watch(userRepositoryProvider);
  return repo.getUser(userId);
}

// Usage: ref.watch(userByIdProvider('user-123'))
```

### KeepAlive

```dart
// By default, generated providers are auto-disposed.
// Use @Riverpod(keepAlive: true) to prevent auto-disposal:

@Riverpod(keepAlive: true)
class AppSettings extends _$AppSettings {
  @override
  Settings build() {
    return const Settings.defaults();
  }
}
```

---

## injectable_generator -- Dependency Injection

### Setup

```bash
flutter pub add injectable
flutter pub add get_it
flutter pub add --dev injectable_generator
flutter pub add --dev build_runner
```

### Configuration

```dart
// file: lib/injection.dart

import 'package:get_it/get_it.dart';
import 'package:injectable/injectable.dart';

import 'injection.config.dart';

final getIt = GetIt.instance;

@InjectableInit()
void configureDependencies() => getIt.init();
```

### Registering Services

```dart
// Singleton (one instance, created eagerly)
@singleton
class ApiClient {
  final Dio dio;

  ApiClient(this.dio);
}

// Lazy Singleton (one instance, created on first access)
@lazySingleton
class AuthRepository {
  final ApiClient _apiClient;

  AuthRepository(this._apiClient);

  Future<User> signIn(String email, String password) async {
    // ...
  }
}

// Factory (new instance each time)
@injectable
class LoginBloc {
  final AuthRepository _authRepository;

  LoginBloc(this._authRepository);
}
```

### Environment-specific Registration

```dart
// Register different implementations for different environments
abstract class AnalyticsService {
  void logEvent(String name, Map<String, dynamic> params);
}

@prod
@LazySingleton(as: AnalyticsService)
class FirebaseAnalyticsService implements AnalyticsService {
  @override
  void logEvent(String name, Map<String, dynamic> params) {
    // Real analytics
  }
}

@test
@LazySingleton(as: AnalyticsService)
class MockAnalyticsService implements AnalyticsService {
  @override
  void logEvent(String name, Map<String, dynamic> params) {
    // No-op for tests
  }
}

// Configure with environment
@InjectableInit()
void configureDependencies(String environment) =>
    getIt.init(environment: environment);

// In main.dart
configureDependencies('prod');

// In tests
configureDependencies('test');
```

### Modules (for third-party dependencies)

```dart
@module
abstract class RegisterModule {
  @lazySingleton
  Dio get dio => Dio(BaseOptions(
    baseUrl: 'https://api.example.com',
    connectTimeout: const Duration(seconds: 10),
  ));

  @lazySingleton
  SharedPreferences get prefs => throw UnimplementedError();

  @preResolve // async dependency
  @lazySingleton
  Future<SharedPreferences> get sharedPreferences =>
      SharedPreferences.getInstance();
}
```

---

## auto_route_generator -- Type-safe Routing

### Setup

```bash
flutter pub add auto_route
flutter pub add --dev auto_route_generator
flutter pub add --dev build_runner
```

### Router Configuration

```dart
// file: lib/router/app_router.dart

import 'package:auto_route/auto_route.dart';

part 'app_router.gr.dart';

@AutoRouterConfig()
class AppRouter extends RootStackRouter {
  @override
  List<AutoRoute> get routes => [
    AutoRoute(page: HomeRoute.page, initial: true),
    AutoRoute(page: LoginRoute.page),
    AutoRoute(page: ProfileRoute.page),
    AutoRoute(
      page: SettingsRoute.page,
      children: [
        AutoRoute(page: GeneralSettingsRoute.page),
        AutoRoute(page: NotificationSettingsRoute.page),
      ],
    ),
    // Guarded route
    AutoRoute(page: AdminRoute.page, guards: [AuthGuard]),
    // Redirect
    RedirectRoute(path: '/sign-in', redirectTo: '/login'),
  ];
}
```

### Annotating Pages

```dart
@RoutePage()
class HomePage extends StatelessWidget {
  const HomePage({super.key});

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      body: Center(
        child: ElevatedButton(
          onPressed: () => context.pushRoute(ProfileRoute(userId: '123')),
          child: const Text('Go to Profile'),
        ),
      ),
    );
  }
}

@RoutePage()
class ProfilePage extends StatelessWidget {
  final String userId;

  const ProfilePage({super.key, @PathParam('id') required this.userId});

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: Text('User $userId')),
    );
  }
}
```

### Route Guards

```dart
class AuthGuard extends AutoRouteGuard {
  final AuthService _authService;

  AuthGuard(this._authService);

  @override
  void onNavigation(NavigationResolver resolver, StackRouter router) {
    if (_authService.isAuthenticated) {
      resolver.next(true);
    } else {
      resolver.redirect(LoginRoute(onResult: (success) {
        resolver.next(success);
      }));
    }
  }
}
```

---

## Combining Multiple Generators

A typical `pubspec.yaml` with multiple generators:

```yaml
dependencies:
  auto_route: ^9.0.0
  freezed_annotation: ^2.4.0
  flutter_riverpod: ^2.5.0
  get_it: ^8.0.0
  injectable: ^2.5.0
  json_annotation: ^4.9.0
  riverpod_annotation: ^2.5.0

dev_dependencies:
  auto_route_generator: ^9.0.0
  build_runner: ^2.4.0
  freezed: ^2.5.0
  injectable_generator: ^2.7.0
  json_serializable: ^6.8.0
  riverpod_generator: ^2.5.0
  riverpod_lint: ^2.5.0
  custom_lint: ^0.7.0
```

Run all generators at once:

```bash
dart run build_runner build --delete-conflicting-outputs
```

---

## build.yaml Configuration

Create `build.yaml` in the project root to customize generator behavior:

```yaml
targets:
  $default:
    builders:
      # Configure json_serializable defaults
      json_serializable:
        options:
          any_map: false
          checked: true
          create_factory: true
          create_to_json: true
          disallow_unrecognized_keys: false
          explicit_to_json: true
          field_rename: snake
          include_if_null: false

      # Configure freezed
      freezed:
        options:
          # Make all freezed classes immutable by default
          immutable: true
          # Generate copyWith for all classes
          copy_with: true
          # Generate == and hashCode
          equal: true
          # Generate toString
          to_string: true
          # Use smart_union for union types
          union_key: "runtimeType"
          union_value_case: snake

      # Control which files are processed
      source_gen|combining_builder:
        options:
          ignore_for_file:
            - type=lint
```

### Filtering Build Targets

```yaml
targets:
  $default:
    sources:
      # Only process files in these directories
      include:
        - lib/**
        - test/**
      # Exclude generated files from being re-processed
      exclude:
        - lib/generated/**
```

---

## Troubleshooting Common Issues

### "Conflicting outputs" Error

```bash
# Solution: use --delete-conflicting-outputs
dart run build_runner build --delete-conflicting-outputs
```

### Generated Files Out of Date

```bash
# Full clean and rebuild
dart run build_runner clean
dart run build_runner build --delete-conflicting-outputs
```

### "Could not find a file named 'X.g.dart'" or Missing Part Files

Ensure you have the correct `part` directives in your source file:

```dart
// For json_serializable:
part 'my_model.g.dart';

// For freezed:
part 'my_model.freezed.dart';
part 'my_model.g.dart';  // only if using @JsonSerializable with freezed

// For auto_route:
part 'app_router.gr.dart';
```

### Build Takes Too Long

```bash
# Use build filters to only generate for specific files
dart run build_runner build --build-filter="lib/models/*"

# Use watch mode during development to avoid full rebuilds
dart run build_runner watch --delete-conflicting-outputs
```

### Version Conflicts Between Generators

Check that all generator packages are compatible:

```bash
flutter pub outdated
flutter pub deps --style=tree
```

Common resolution:

```bash
# Reset and re-resolve dependencies
flutter clean
rm pubspec.lock
flutter pub get
```

### "Bad state: Duplicate builder" Error

This occurs when multiple packages register the same builder. Check `build.yaml` for duplicate builder registrations and ensure you do not have conflicting versions of generator packages.

### Part File Not Found After Rename

After renaming a file, the old `.g.dart` / `.freezed.dart` files remain:

```bash
# Clean all generated files and rebuild
dart run build_runner clean
dart run build_runner build --delete-conflicting-outputs
```

### Hot Reload Not Picking Up Generated Files

Generated files are created at build time. If you change an annotation, you must re-run build_runner. Use watch mode to handle this automatically:

```bash
dart run build_runner watch --delete-conflicting-outputs
```
