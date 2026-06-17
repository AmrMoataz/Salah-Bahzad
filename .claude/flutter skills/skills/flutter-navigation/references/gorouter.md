# go_router Compatibility Guide (Optional in Flutter-Style Projects)

## Positioning

Flutter defaults to centralized route constants + route mapper patterns. Use `go_router` only when URL-driven routing, deep links, or web-first typed route ergonomics are required.

## Minimal Setup

```yaml
dependencies:
  go_router: ^14.0.0
```

```dart
final router = GoRouter(
  initialLocation: '/',
  routes: [
    GoRoute(path: '/', builder: (context, state) => const HomeScreen()),
    GoRoute(path: '/login', builder: (context, state) => const LoginScreen()),
  ],
);
```

## Declarative Guard Pattern

```dart
GoRouter createRouter(ValueListenable<bool> isAuthenticated) {
  return GoRouter(
    refreshListenable: isAuthenticated,
    redirect: (context, state) {
      final loggedIn = isAuthenticated.value;
      final isLogin = state.matchedLocation == '/login';
      if (!loggedIn && !isLogin) return '/login';
      if (loggedIn && isLogin) return '/';
      return null;
    },
    routes: [
      GoRoute(path: '/', builder: (context, state) => const HomeScreen()),
      GoRoute(path: '/login', builder: (context, state) => const LoginScreen()),
    ],
  );
}
```

## Usage Rules

1. Keep route names centralized and reusable.
2. Avoid mixing raw route strings throughout widgets.
3. Keep redirects synchronous and derived from cached/session state.
4. Keep guard logic consistent with Bloc/session ownership rules.
