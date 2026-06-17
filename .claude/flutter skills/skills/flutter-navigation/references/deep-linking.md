# Deep Linking and ShellRoute

## 1. ShellRoute -- Persistent Bottom Navigation

`ShellRoute` wraps child routes in a shared layout (e.g., a `Scaffold` with
a `BottomNavigationBar`). The shell stays mounted while the child content
swaps.

```dart
// lib/router/app_router.dart
import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

final GoRouter appRouter = GoRouter(
  initialLocation: '/home',
  routes: [
    ShellRoute(
      builder: (context, state, child) {
        return ScaffoldWithNav(child: child);
      },
      routes: [
        GoRoute(
          path: '/home',
          name: 'home',
          builder: (context, state) => const HomeTab(),
        ),
        GoRoute(
          path: '/explore',
          name: 'explore',
          builder: (context, state) => const ExploreTab(),
        ),
        GoRoute(
          path: '/profile',
          name: 'profile',
          builder: (context, state) => const ProfileTab(),
        ),
      ],
    ),
    // Routes outside the shell (e.g., login) have no bottom nav
    GoRoute(
      path: '/login',
      name: 'login',
      builder: (context, state) => const LoginScreen(),
    ),
  ],
);
```

### ScaffoldWithNav Widget

```dart
class ScaffoldWithNav extends StatelessWidget {
  const ScaffoldWithNav({required this.child, super.key});

  final Widget child;

  static const _tabs = ['/home', '/explore', '/profile'];

  int _currentIndex(BuildContext context) {
    final location = GoRouterState.of(context).matchedLocation;
    final index = _tabs.indexWhere(location.startsWith);
    return index < 0 ? 0 : index;
  }

  @override
  Widget build(BuildContext context) {
    final selectedIndex = _currentIndex(context);

    return Scaffold(
      body: child,
      bottomNavigationBar: NavigationBar(
        selectedIndex: selectedIndex,
        onDestinationSelected: (i) => context.go(_tabs[i]),
        destinations: const [
          NavigationDestination(icon: Icon(Icons.home), label: 'Home'),
          NavigationDestination(icon: Icon(Icons.explore), label: 'Explore'),
          NavigationDestination(icon: Icon(Icons.person), label: 'Profile'),
        ],
      ),
    );
  }
}
```

---

## 2. StatefulShellRoute -- Preserving Tab State

`StatefulShellRoute` keeps each branch's navigation stack alive when switching
tabs, so users do not lose scroll position or sub-page state.

```dart
import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

final _rootNavigatorKey = GlobalKey<NavigatorState>();
final _homeNavigatorKey = GlobalKey<NavigatorState>(debugLabel: 'home');
final _exploreNavigatorKey = GlobalKey<NavigatorState>(debugLabel: 'explore');
final _profileNavigatorKey = GlobalKey<NavigatorState>(debugLabel: 'profile');

final GoRouter appRouter = GoRouter(
  navigatorKey: _rootNavigatorKey,
  initialLocation: '/home',
  routes: [
    StatefulShellRoute.indexedStack(
      builder: (context, state, navigationShell) {
        return ScaffoldWithStatefulNav(navigationShell: navigationShell);
      },
      branches: [
        StatefulShellBranch(
          navigatorKey: _homeNavigatorKey,
          routes: [
            GoRoute(
              path: '/home',
              name: 'home',
              builder: (context, state) => const HomeTab(),
              routes: [
                GoRoute(
                  path: 'detail/:id',
                  name: 'home-detail',
                  builder: (context, state) {
                    final id = state.pathParameters['id']!;
                    return DetailScreen(id: id);
                  },
                ),
              ],
            ),
          ],
        ),
        StatefulShellBranch(
          navigatorKey: _exploreNavigatorKey,
          routes: [
            GoRoute(
              path: '/explore',
              name: 'explore',
              builder: (context, state) => const ExploreTab(),
              routes: [
                GoRoute(
                  path: 'category/:name',
                  name: 'explore-category',
                  builder: (context, state) {
                    final name = state.pathParameters['name']!;
                    return CategoryScreen(name: name);
                  },
                ),
              ],
            ),
          ],
        ),
        StatefulShellBranch(
          navigatorKey: _profileNavigatorKey,
          routes: [
            GoRoute(
              path: '/profile',
              name: 'profile',
              builder: (context, state) => const ProfileTab(),
              routes: [
                GoRoute(
                  path: 'settings',
                  name: 'profile-settings',
                  builder: (context, state) => const SettingsScreen(),
                ),
              ],
            ),
          ],
        ),
      ],
    ),
  ],
);
```

### ScaffoldWithStatefulNav Widget

```dart
class ScaffoldWithStatefulNav extends StatelessWidget {
  const ScaffoldWithStatefulNav({
    required this.navigationShell,
    super.key,
  });

  final StatefulNavigationShell navigationShell;

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      body: navigationShell,
      bottomNavigationBar: NavigationBar(
        selectedIndex: navigationShell.currentIndex,
        onDestinationSelected: (index) {
          // goBranch navigates to the branch's last known location
          navigationShell.goBranch(
            index,
            initialLocation: index == navigationShell.currentIndex,
          );
        },
        destinations: const [
          NavigationDestination(icon: Icon(Icons.home), label: 'Home'),
          NavigationDestination(icon: Icon(Icons.explore), label: 'Explore'),
          NavigationDestination(icon: Icon(Icons.person), label: 'Profile'),
        ],
      ),
    );
  }
}
```

---

## 3. Nested Navigation within Tabs

Each `StatefulShellBranch` owns its own `Navigator`. Pushing a sub-route
(e.g., `/home/detail/5`) adds to that branch's stack while keeping the bottom
bar visible.

```dart
// Inside the Home tab
class HomeTab extends StatelessWidget {
  const HomeTab({super.key});

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Home')),
      body: ListView.builder(
        itemCount: 20,
        itemBuilder: (context, index) {
          return ListTile(
            title: Text('Item $index'),
            onTap: () => context.go('/home/detail/$index'),
          );
        },
      ),
    );
  }
}
```

To push a route **outside** the shell (e.g., a full-screen dialog that hides
the bottom bar), use the root navigator key with `parentNavigatorKey`:

```dart
GoRoute(
  parentNavigatorKey: _rootNavigatorKey,
  path: '/fullscreen-image/:url',
  name: 'fullscreen-image',
  builder: (context, state) {
    final url = state.pathParameters['url']!;
    return FullScreenImageViewer(url: Uri.decodeComponent(url));
  },
),
```

---

## 4. Deep Linking Setup

### iOS -- Universal Links

1. **Enable Associated Domains** in Xcode:

   `Signing & Capabilities` -> `+ Capability` -> `Associated Domains`

   Add: `applinks:example.com`

2. **Host the Apple App Site Association file** at
   `https://example.com/.well-known/apple-app-site-association`:

```json
{
  "applinks": {
    "apps": [],
    "details": [
      {
        "appID": "TEAMID.com.example.myapp",
        "paths": ["/products/*", "/profile/*"]
      }
    ]
  }
}
```

3. **Handle in Flutter** -- GoRouter handles incoming URIs automatically when
   `MaterialApp.router` is used. No additional code is required.

### Android -- App Links

1. Add an `<intent-filter>` to `android/app/src/main/AndroidManifest.xml`:

```xml
<activity
    android:name=".MainActivity"
    android:launchMode="singleTop">

    <!-- Deep link intent filter -->
    <intent-filter android:autoVerify="true">
        <action android:name="android.intent.action.VIEW" />
        <category android:name="android.intent.category.DEFAULT" />
        <category android:name="android.intent.category.BROWSABLE" />
        <data
            android:scheme="https"
            android:host="example.com"
            android:pathPrefix="/products" />
        <data
            android:scheme="https"
            android:host="example.com"
            android:pathPrefix="/profile" />
    </intent-filter>
</activity>
```

2. **Host the Digital Asset Links file** at
   `https://example.com/.well-known/assetlinks.json`:

```json
[
  {
    "relation": ["delegate_permission/common.handle_all_urls"],
    "target": {
      "namespace": "android_app",
      "package_name": "com.example.myapp",
      "sha256_cert_fingerprints": [
        "AA:BB:CC:DD:EE:FF:00:11:22:33:44:55:66:77:88:99:AA:BB:CC:DD:EE:FF:00:11:22:33:44:55:66:77:88:99"
      ]
    }
  }
]
```

---

## 5. Web URL Strategy (PathUrlStrategy)

By default Flutter web uses hash-based URLs (`/#/products`). Switch to clean
path-based URLs for SEO and deep linking:

```dart
// lib/main.dart
import 'package:flutter/material.dart';
import 'package:flutter_web_plugins/flutter_web_plugins.dart';
import 'router/app_router.dart';

void main() {
  usePathUrlStrategy();
  runApp(const MyApp());
}
```

> Ensure your web server rewrites all paths to `index.html` so that direct
> navigation to `/products/42` does not produce a 404.

---

## 6. Testing Deep Links

### Unit Test -- Route Matching

```dart
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:my_app/router/app_router.dart';

void main() {
  test('/ maps to HomeScreen', () {
    final matches = appRouter.configuration.findMatch('/');
    expect(matches.uri.toString(), '/');
  });

  test('/products/:id maps to ProductDetailScreen', () {
    final matches = appRouter.configuration.findMatch('/products/42');
    expect(matches.pathParameters['id'], '42');
  });
}
```

### Widget Test -- Deep Link Navigation

```dart
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';

void main() {
  testWidgets('navigates to product detail via deep link', (tester) async {
    final router = GoRouter(
      initialLocation: '/products/42',
      routes: [
        GoRoute(
          path: '/',
          builder: (context, state) => const Scaffold(body: Text('Home')),
          routes: [
            GoRoute(
              path: 'products/:id',
              builder: (context, state) {
                final id = state.pathParameters['id']!;
                return Scaffold(body: Text('Product $id'));
              },
            ),
          ],
        ),
      ],
    );

    await tester.pumpWidget(MaterialApp.router(routerConfig: router));
    await tester.pumpAndSettle();

    expect(find.text('Product 42'), findsOneWidget);
  });
}
```

### CLI Verification

```bash
# Android -- launch deep link from terminal
adb shell am start \
  -a android.intent.action.VIEW \
  -d "https://example.com/products/42" \
  com.example.myapp

# iOS -- launch deep link from terminal (simulator)
xcrun simctl openurl booted "https://example.com/products/42"
```
