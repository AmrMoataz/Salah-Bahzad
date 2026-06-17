# Page Transitions

## 1. CustomTransitionPage Basics

GoRouter uses `CustomTransitionPage` to override the default platform
transition for individual routes.

```dart
import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

GoRoute(
  path: '/detail/:id',
  name: 'detail',
  pageBuilder: (context, state) {
    final id = state.pathParameters['id']!;
    return CustomTransitionPage(
      key: state.pageKey,
      child: DetailScreen(id: id),
      transitionsBuilder: (context, animation, secondaryAnimation, child) {
        return FadeTransition(opacity: animation, child: child);
      },
    );
  },
),
```

> Use `pageBuilder` instead of `builder` whenever you need a custom transition.

---

## 2. Fade Transition

```dart
CustomTransitionPage fadeTransitionPage({
  required LocalKey key,
  required Widget child,
  Duration duration = const Duration(milliseconds: 300),
}) {
  return CustomTransitionPage(
    key: key,
    child: child,
    transitionDuration: duration,
    reverseTransitionDuration: duration,
    transitionsBuilder: (context, animation, secondaryAnimation, child) {
      return FadeTransition(opacity: animation, child: child);
    },
  );
}

// Usage in a route
GoRoute(
  path: '/about',
  name: 'about',
  pageBuilder: (context, state) => fadeTransitionPage(
    key: state.pageKey,
    child: const AboutScreen(),
  ),
),
```

---

## 3. Slide Transition

### Slide from Right (default forward direction)

```dart
CustomTransitionPage slideTransitionPage({
  required LocalKey key,
  required Widget child,
  Offset beginOffset = const Offset(1.0, 0.0),
  Duration duration = const Duration(milliseconds: 300),
}) {
  return CustomTransitionPage(
    key: key,
    child: child,
    transitionDuration: duration,
    reverseTransitionDuration: duration,
    transitionsBuilder: (context, animation, secondaryAnimation, child) {
      final tween = Tween(begin: beginOffset, end: Offset.zero).chain(
        CurveTween(curve: Curves.easeInOut),
      );
      return SlideTransition(position: animation.drive(tween), child: child);
    },
  );
}
```

### Slide from Bottom

```dart
GoRoute(
  path: '/modal',
  name: 'modal',
  pageBuilder: (context, state) => slideTransitionPage(
    key: state.pageKey,
    child: const ModalScreen(),
    beginOffset: const Offset(0.0, 1.0), // Slide up from bottom
  ),
),
```

---

## 4. Scale Transition

```dart
CustomTransitionPage scaleTransitionPage({
  required LocalKey key,
  required Widget child,
  Duration duration = const Duration(milliseconds: 300),
  Alignment alignment = Alignment.center,
}) {
  return CustomTransitionPage(
    key: key,
    child: child,
    transitionDuration: duration,
    reverseTransitionDuration: duration,
    transitionsBuilder: (context, animation, secondaryAnimation, child) {
      final curved = CurvedAnimation(
        parent: animation,
        curve: Curves.easeOutBack,
      );
      return ScaleTransition(
        scale: curved,
        alignment: alignment,
        child: FadeTransition(opacity: animation, child: child),
      );
    },
  );
}

// Usage
GoRoute(
  path: '/popup',
  name: 'popup',
  pageBuilder: (context, state) => scaleTransitionPage(
    key: state.pageKey,
    child: const PopupScreen(),
  ),
),
```

---

## 5. Platform-Adaptive Transitions (Cupertino vs Material)

Automatically pick the right transition based on the platform:

```dart
import 'dart:io' show Platform;
import 'package:flutter/cupertino.dart';
import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

CustomTransitionPage adaptiveTransitionPage({
  required LocalKey key,
  required Widget child,
}) {
  return CustomTransitionPage(
    key: key,
    child: child,
    transitionsBuilder: (context, animation, secondaryAnimation, child) {
      final platform = Theme.of(context).platform;

      if (platform == TargetPlatform.iOS ||
          platform == TargetPlatform.macOS) {
        // iOS-style slide with parallax
        return CupertinoPageTransition(
          primaryRouteAnimation: animation,
          secondaryRouteAnimation: secondaryAnimation,
          linearTransition: false,
          child: child,
        );
      }

      // Material fade-through
      return FadeTransition(
        opacity: CurvedAnimation(
          parent: animation,
          curve: Curves.easeInOut,
        ),
        child: child,
      );
    },
  );
}
```

### Applying Globally via a Helper

Wrap every `GoRoute` with this helper to get adaptive transitions app-wide:

```dart
GoRoute adaptiveRoute({
  required String path,
  required String name,
  required Widget Function(BuildContext, GoRouterState) childBuilder,
  List<RouteBase> routes = const [],
}) {
  return GoRoute(
    path: path,
    name: name,
    routes: routes,
    pageBuilder: (context, state) => adaptiveTransitionPage(
      key: state.pageKey,
      child: childBuilder(context, state),
    ),
  );
}

// Usage
adaptiveRoute(
  path: '/settings',
  name: 'settings',
  childBuilder: (context, state) => const SettingsScreen(),
),
```

---

## 6. Shared Element / Hero Transitions with GoRouter

Flutter's `Hero` widget works across GoRouter navigations as long as both the
source and destination widgets share the same `Hero` tag.

### Source Screen

```dart
class ProductListScreen extends StatelessWidget {
  const ProductListScreen({super.key});

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Products')),
      body: GridView.builder(
        padding: const EdgeInsets.all(16),
        gridDelegate: const SliverGridDelegateWithFixedCrossAxisCount(
          crossAxisCount: 2,
          mainAxisSpacing: 16,
          crossAxisSpacing: 16,
        ),
        itemCount: 20,
        itemBuilder: (context, index) {
          final tag = 'product-image-$index';
          return GestureDetector(
            onTap: () => context.push('/products/$index'),
            child: Hero(
              tag: tag,
              child: ClipRRect(
                borderRadius: BorderRadius.circular(12),
                child: Image.network(
                  'https://picsum.photos/seed/$index/200',
                  fit: BoxFit.cover,
                ),
              ),
            ),
          );
        },
      ),
    );
  }
}
```

### Destination Screen

```dart
class ProductDetailScreen extends StatelessWidget {
  const ProductDetailScreen({required this.productId, super.key});

  final String productId;

  @override
  Widget build(BuildContext context) {
    final tag = 'product-image-$productId';
    return Scaffold(
      appBar: AppBar(title: Text('Product $productId')),
      body: Column(
        children: [
          Hero(
            tag: tag,
            child: SizedBox(
              height: 300,
              width: double.infinity,
              child: Image.network(
                'https://picsum.photos/seed/$productId/400',
                fit: BoxFit.cover,
              ),
            ),
          ),
          Padding(
            padding: const EdgeInsets.all(16),
            child: Text(
              'Details for product $productId',
              style: Theme.of(context).textTheme.headlineSmall,
            ),
          ),
        ],
      ),
    );
  }
}
```

### Route Definition (Hero-Friendly)

Use `context.push` (not `context.go`) so that both the source and destination
pages are in the widget tree simultaneously during the animation:

```dart
GoRoute(
  path: '/products',
  name: 'products',
  builder: (context, state) => const ProductListScreen(),
  routes: [
    GoRoute(
      path: ':id',
      name: 'product-detail',
      pageBuilder: (context, state) {
        final id = state.pathParameters['id']!;
        return CustomTransitionPage(
          key: state.pageKey,
          child: ProductDetailScreen(productId: id),
          transitionDuration: const Duration(milliseconds: 400),
          reverseTransitionDuration: const Duration(milliseconds: 400),
          transitionsBuilder:
              (context, animation, secondaryAnimation, child) {
            return FadeTransition(opacity: animation, child: child);
          },
        );
      },
    ),
  ],
),
```

> **Tip**: For Hero animations, pair `context.push` with a fade or no-op
> transition so the Hero widget controls the visual movement.

---

## 7. No-Animation Navigation

Useful for tab switches or routes that should appear instantly:

```dart
CustomTransitionPage noTransitionPage({
  required LocalKey key,
  required Widget child,
}) {
  return CustomTransitionPage(
    key: key,
    child: child,
    transitionDuration: Duration.zero,
    reverseTransitionDuration: Duration.zero,
    transitionsBuilder: (context, animation, secondaryAnimation, child) {
      return child;
    },
  );
}

// Usage
GoRoute(
  path: '/tab-content',
  name: 'tab-content',
  pageBuilder: (context, state) => noTransitionPage(
    key: state.pageKey,
    child: const TabContentScreen(),
  ),
),
```

### Using NoTransitionPage (Built-In)

GoRouter also ships `NoTransitionPage` for the same purpose:

```dart
GoRoute(
  path: '/instant',
  name: 'instant',
  pageBuilder: (context, state) => NoTransitionPage(
    key: state.pageKey,
    child: const InstantScreen(),
  ),
),
```
