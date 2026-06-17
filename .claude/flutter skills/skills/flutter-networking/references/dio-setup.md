# Dio Setup in Flutter-Style Architecture

## Core Principle

Dio lives in the data boundary and is consumed through service/repository abstractions. Presentation and UI never depend on raw HTTP behavior.

## Minimal Configuration

```dart
@module
abstract class NetworkModule {
  @lazySingleton
  Dio dio(BaseUrlProvider baseUrlProvider) {
    final dio = Dio(
      BaseOptions(
        baseUrl: baseUrlProvider.value,
        connectTimeout: const Duration(seconds: 15),
        receiveTimeout: const Duration(seconds: 15),
        sendTimeout: const Duration(seconds: 15),
        headers: const {
          'Accept': 'application/json',
          'Content-Type': 'application/json',
        },
      ),
    );
    return dio;
  }
}
```

## Typed Failure Mapping

```dart
sealed class NetworkFailure {
  const NetworkFailure(this.message);
  final String message;
}

final class UnauthorizedFailure extends NetworkFailure {
  const UnauthorizedFailure() : super('Unauthorized');
}

final class TimeoutFailure extends NetworkFailure {
  const TimeoutFailure() : super('Request timeout');
}

final class UnknownFailure extends NetworkFailure {
  const UnknownFailure(String message) : super(message);
}

NetworkFailure mapDioException(DioException e) {
  if (e.response?.statusCode == 401) return const UnauthorizedFailure();
  if (e.type == DioExceptionType.connectionTimeout ||
      e.type == DioExceptionType.receiveTimeout ||
      e.type == DioExceptionType.sendTimeout) {
    return const TimeoutFailure();
  }
  return UnknownFailure(e.message ?? 'Unexpected error');
}
```

## Service and Repository Boundary

```dart
abstract class AuthService {
  Future<DataResult<UserModel>> login(String email, String password);
}

class AuthServiceImpl implements AuthService {
  AuthServiceImpl(this.dio);
  final Dio dio;

  @override
  Future<DataResult<UserModel>> login(String email, String password) async {
    try {
      final response = await dio.post<Map<String, dynamic>>(
        '/auth/login',
        data: {'email': email, 'password': password},
      );
      return Success(UserModel.fromJson(response.data ?? {}));
    } on DioException catch (e) {
      return Error(mapDioException(e).message);
    }
  }
}
```

## Interceptor Rules

1. Add auth headers and refresh logic in interceptors, not in widgets/blocs.
2. Redact sensitive data in logs.
3. Keep retry policy conservative and idempotency-aware.

## Cancellation Rule

Use `CancelToken` for user-driven cancellation paths such as live search.
