# Auth Guard Patterns (Flutter-Aligned)

## Default Guard Inputs

Route guard decisions should come from cached auth/session state managed by Bloc or equivalent session coordinator.

## Guard Policy

1. Unauthenticated users go to login.
2. Authenticated users cannot stay on login/splash routes.
3. Role-protected routes evaluate role synchronously.
4. Redirect code remains synchronous and side-effect free.

## Example Policy Function

```dart
String? resolveRedirect({
  required bool isAuthenticated,
  required bool isUnknown,
  required bool isLogin,
  required bool isSplash,
  required bool hasAdminRole,
  required bool wantsAdminRoute,
}) {
  if (isUnknown) return isSplash ? null : '/splash';
  if (!isAuthenticated) return isLogin ? null : '/login';
  if ((isLogin || isSplash)) return '/';
  if (wantsAdminRoute && !hasAdminRole) return '/unauthorized';
  return null;
}
```

## Bloc-Compatible Refresh Bridge

When router refresh requires a listenable, adapt Bloc stream to a `ChangeNotifier` and keep mapping logic in a single function.

## Anti-Patterns

1. Async calls inside redirect callbacks.
2. Scattered route role checks in many widgets.
3. Using route guards as a replacement for backend authorization.
