# Riverpod 2.0+ Reference Guide

## Overview

Riverpod is a reactive caching and data-binding framework for Dart and Flutter.
Version 2.0+ introduced `Notifier` / `AsyncNotifier` as replacements for
`StateNotifier`, and the `riverpod_generator` package for code-generated
providers. This reference covers the full surface area needed for production
Flutter applications.

---

## Table of Contents

1. [Provider Types](#provider-types)
2. [Code Generation with @riverpod](#code-generation-with-riverpod)
3. [Family Providers (Parameterized)](#family-providers-parameterized)
4. [Provider Lifecycle and Auto-Dispose](#provider-lifecycle-and-auto-dispose)
5. [Selective Rebuilds with .select()](#selective-rebuilds-with-select)
6. [Ref Methods](#ref-methods)
7. [Widget Integration](#widget-integration)
8. [Testing Riverpod](#testing-riverpod)
9. [Common Patterns](#common-patterns)

---

## Provider Types

### Provider (read-only, synchronous)

Returns a value that never changes from the provider's perspective. Useful for
exposing service instances or computed configuration.

```dart
import 'package:riverpod_annotation/riverpod_annotation.dart';

part 'api_client.g.dart';

@riverpod
ApiClient apiClient(Ref ref) {
  final baseUrl = ref.watch(baseUrlProvider);
  return ApiClient(baseUrl: baseUrl);
}
```

Manual equivalent (without code gen):

```dart
final apiClientProvider = Provider<ApiClient>((ref) {
  final baseUrl = ref.watch(baseUrlProvider);
  return ApiClient(baseUrl: baseUrl);
});
```

### StateProvider (simple mutable state)

Best for trivial state like a counter, a toggle, or a selected enum value.

```dart
final counterProvider = StateProvider<int>((ref) => 0);
```

Usage:

```dart
// Read
final count = ref.watch(counterProvider);

// Mutate
ref.read(counterProvider.notifier).state++;
```

> **Prefer `NotifierProvider`** for anything beyond the most trivial cases.
> `StateProvider` has no place for logic.

### FutureProvider (async, read-only)

Exposes the result of a single asynchronous computation.

```dart
@riverpod
Future<List<Product>> products(Ref ref) async {
  final client = ref.watch(apiClientProvider);
  return client.fetchProducts();
}
```

### StreamProvider

Exposes a stream as an `AsyncValue`.

```dart
@riverpod
Stream<User> authState(Ref ref) {
  final auth = ref.watch(firebaseAuthProvider);
  return auth.authStateChanges();
}
```

### NotifierProvider (synchronous, stateful)

Replacement for the deprecated `StateNotifierProvider`. Holds synchronous
mutable state with encapsulated logic.

```dart
@riverpod
class Counter extends _$Counter {
  @override
  int build() => 0;

  void increment() => state++;
  void decrement() => state--;
  void reset() => state = 0;
}
```

### AsyncNotifierProvider (async, stateful)

Same as `NotifierProvider` but `build()` returns a `Future`.

```dart
@riverpod
class ProductList extends _$ProductList {
  @override
  Future<List<Product>> build() async {
    final client = ref.watch(apiClientProvider);
    return client.fetchProducts();
  }

  Future<void> refresh() async {
    state = const AsyncLoading();
    state = await AsyncValue.guard(() async {
      final client = ref.read(apiClientProvider);
      return client.fetchProducts();
    });
  }

  Future<void> add(Product product) async {
    final client = ref.read(apiClientProvider);
    await client.createProduct(product);
    ref.invalidateSelf();
  }
}
```

---

## Code Generation with @riverpod

### Setup

Add to `pubspec.yaml`:

```yaml
dependencies:
  flutter_riverpod: ^2.5.0
  riverpod_annotation: ^2.3.0

dev_dependencies:
  build_runner: ^2.4.0
  riverpod_generator: ^2.4.0
  riverpod_lint: ^2.3.0
```

Run the generator:

```bash
dart run build_runner watch --delete-conflicting-outputs
```

### Annotation Forms

```dart
// Functional provider (immutable / computed)
@riverpod
String greeting(Ref ref) {
  final name = ref.watch(nameProvider);
  return 'Hello, $name!';
}

// Class-based provider (mutable state)
@riverpod
class TodoList extends _$TodoList {
  @override
  List<Todo> build() => [];

  void add(Todo todo) {
    state = [...state, todo];
  }

  void remove(String id) {
    state = state.where((t) => t.id != id).toList();
  }

  void toggle(String id) {
    state = [
      for (final todo in state)
        if (todo.id == id) todo.copyWith(done: !todo.done) else todo,
    ];
  }
}
```

### keepAlive

By default, code-generated providers are auto-disposed. Use `@Riverpod(keepAlive: true)` to keep the provider alive for the entire app lifetime.

```dart
@Riverpod(keepAlive: true)
class AuthController extends _$AuthController {
  @override
  Future<User?> build() async {
    return ref.watch(authRepositoryProvider).currentUser;
  }

  Future<void> signIn(String email, String password) async {
    state = const AsyncLoading();
    state = await AsyncValue.guard(
      () => ref.read(authRepositoryProvider).signIn(email, password),
    );
  }

  Future<void> signOut() async {
    await ref.read(authRepositoryProvider).signOut();
    state = const AsyncData(null);
  }
}
```

---

## Family Providers (Parameterized)

Family providers accept parameters so you can create a provider per unique
argument.

### Code-gen approach (preferred)

Simply add parameters to the function or `build` method:

```dart
@riverpod
Future<Product> product(Ref ref, {required String productId}) async {
  final client = ref.watch(apiClientProvider);
  return client.fetchProduct(productId);
}
```

Usage:

```dart
final product = ref.watch(productProvider(productId: '42'));
```

### Class-based family

```dart
@riverpod
class ProductDetail extends _$ProductDetail {
  @override
  Future<Product> build({required String productId}) async {
    final client = ref.watch(apiClientProvider);
    return client.fetchProduct(productId);
  }

  Future<void> updateQuantity(int quantity) async {
    final current = await future;
    final updated = current.copyWith(quantity: quantity);
    state = AsyncData(updated);
    await ref.read(apiClientProvider).updateProduct(updated);
  }
}
```

### Manual family (without code gen)

```dart
final productProvider =
    FutureProvider.family.autoDispose<Product, String>((ref, productId) async {
  final client = ref.watch(apiClientProvider);
  return client.fetchProduct(productId);
});
```

---

## Provider Lifecycle and Auto-Dispose

### Auto-dispose (default with code gen)

When no widget or provider is watching, the provider is disposed and its state is
discarded. Next time it is watched, `build()` runs again.

### keepAlive at runtime

Inside a provider you can dynamically decide to keep the state alive using
`ref.keepAlive()`. This is useful for caching:

```dart
@riverpod
Future<Article> article(Ref ref, {required String slug}) async {
  // Keep the fetched article cached even when the widget is off-screen.
  final link = ref.keepAlive();

  // But dispose it after 5 minutes of inactivity.
  final timer = Timer(const Duration(minutes: 5), link.close);
  ref.onDispose(timer.cancel);

  final client = ref.watch(apiClientProvider);
  return client.fetchArticle(slug);
}
```

### Disposal callbacks

```dart
@riverpod
StreamController<int> eventBus(Ref ref) {
  final controller = StreamController<int>.broadcast();
  ref.onDispose(controller.close);
  return controller;
}
```

---

## Selective Rebuilds with .select()

Use `.select()` to rebuild a widget only when a specific part of the state
changes:

```dart
class CartBadge extends ConsumerWidget {
  const CartBadge({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    // Only rebuilds when the item count changes, not when items themselves
    // change (e.g., quantity update on an existing item).
    final itemCount = ref.watch(
      cartProvider.select((cart) => cart.items.length),
    );

    return Badge(
      label: Text('$itemCount'),
      child: const Icon(Icons.shopping_cart),
    );
  }
}
```

Works on `AsyncValue` too:

```dart
final userName = ref.watch(
  userProvider.select(
    (asyncUser) => asyncUser.whenData((user) => user.name),
  ),
);
```

---

## Ref Methods

| Method              | Purpose                                                     |
| ------------------- | ----------------------------------------------------------- |
| `ref.watch(p)`      | Subscribe to a provider; rebuilds when it changes.          |
| `ref.read(p)`       | Read a provider once without subscribing.                   |
| `ref.listen(p, cb)` | Listen for changes and run a side-effect callback.          |
| `ref.invalidate(p)` | Force a provider to recompute on next read.                 |
| `ref.invalidateSelf()` | Force the current provider to recompute.                 |
| `ref.onDispose(cb)` | Register a teardown callback.                               |
| `ref.keepAlive()`   | Prevent auto-dispose; returns a `KeepAliveLink`.            |
| `ref.exists(p)`     | Check whether a provider has been initialized.              |

### ref.listen example (side effects)

```dart
@riverpod
class Checkout extends _$Checkout {
  @override
  CheckoutState build() {
    ref.listen(connectivityProvider, (previous, next) {
      if (next == ConnectivityStatus.offline) {
        state = state.copyWith(offlineBanner: true);
      }
    });
    return const CheckoutState.initial();
  }
}
```

---

## Widget Integration

### ConsumerWidget

The most common approach. Replaces `StatelessWidget`.

```dart
class ProductTile extends ConsumerWidget {
  const ProductTile({super.key, required this.productId});

  final String productId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final productAsync = ref.watch(productProvider(productId: productId));

    return productAsync.when(
      loading: () => const ShimmerTile(),
      error: (e, st) => ErrorTile(error: e),
      data: (product) => ListTile(
        title: Text(product.name),
        subtitle: Text('\$${product.price.toStringAsFixed(2)}'),
        trailing: IconButton(
          icon: const Icon(Icons.add_shopping_cart),
          onPressed: () =>
              ref.read(cartProvider.notifier).addItem(product),
        ),
      ),
    );
  }
}
```

### Consumer (inline)

Use when only a subtree needs to watch a provider.

```dart
class ProductPage extends StatelessWidget {
  const ProductPage({super.key});

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Products')),
      body: Consumer(
        builder: (context, ref, child) {
          final products = ref.watch(productsProvider);
          return products.when(
            loading: () => const Center(child: CircularProgressIndicator()),
            error: (e, st) => Center(child: Text('Error: $e')),
            data: (list) => ListView.builder(
              itemCount: list.length,
              itemBuilder: (context, index) => ProductTile(
                productId: list[index].id,
              ),
            ),
          );
        },
      ),
    );
  }
}
```

### HookConsumerWidget (with flutter_hooks)

```dart
class SearchPage extends HookConsumerWidget {
  const SearchPage({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final controller = useTextEditingController();
    final debounced = useDebounced(controller.text, const Duration(milliseconds: 400));
    final results = ref.watch(searchProvider(query: debounced ?? ''));

    return Column(
      children: [
        TextField(controller: controller),
        Expanded(
          child: results.when(
            loading: () => const Center(child: CircularProgressIndicator()),
            error: (e, st) => Center(child: Text('Error: $e')),
            data: (items) => ListView.builder(
              itemCount: items.length,
              itemBuilder: (context, i) => ListTile(title: Text(items[i].title)),
            ),
          ),
        ),
      ],
    );
  }
}
```

---

## Testing Riverpod

### Unit testing a Notifier

```dart
import 'package:flutter_test/flutter_test.dart';
import 'package:riverpod/riverpod.dart';

void main() {
  group('Counter', () {
    late ProviderContainer container;

    setUp(() {
      container = ProviderContainer();
    });

    tearDown(() {
      container.dispose();
    });

    test('initial value is 0', () {
      expect(container.read(counterProvider), 0);
    });

    test('increment adds 1', () {
      container.read(counterProvider.notifier).increment();
      expect(container.read(counterProvider), 1);
    });

    test('decrement subtracts 1', () {
      container.read(counterProvider.notifier).increment();
      container.read(counterProvider.notifier).decrement();
      expect(container.read(counterProvider), 0);
    });
  });
}
```

### Overriding providers (dependency injection in tests)

```dart
void main() {
  group('ProductList', () {
    late ProviderContainer container;

    setUp(() {
      container = ProviderContainer(
        overrides: [
          apiClientProvider.overrideWithValue(MockApiClient()),
        ],
      );
    });

    tearDown(() {
      container.dispose();
    });

    test('fetches products from API', () async {
      final products = await container.read(productListProvider.future);
      expect(products, hasLength(3));
    });
  });
}

class MockApiClient extends ApiClient {
  @override
  Future<List<Product>> fetchProducts() async {
    return [
      Product(id: '1', name: 'Widget', price: 9.99),
      Product(id: '2', name: 'Gadget', price: 19.99),
      Product(id: '3', name: 'Gizmo', price: 29.99),
    ];
  }
}
```

### Widget testing with ProviderScope overrides

```dart
void main() {
  testWidgets('ProductTile shows product name', (tester) async {
    await tester.pumpWidget(
      ProviderScope(
        overrides: [
          productProvider(productId: '1').overrideWith(
            (ref) => const Product(id: '1', name: 'Test Item', price: 5.0),
          ),
        ],
        child: const MaterialApp(
          home: ProductTile(productId: '1'),
        ),
      ),
    );

    expect(find.text('Test Item'), findsOneWidget);
    expect(find.text('\$5.00'), findsOneWidget);
  });
}
```

### Listening to state changes in tests

```dart
test('emits states in order', () async {
  final container = ProviderContainer(
    overrides: [apiClientProvider.overrideWithValue(MockApiClient())],
  );
  addTearDown(container.dispose);

  final states = <AsyncValue<List<Product>>>[];
  container.listen(productListProvider, (prev, next) {
    states.add(next);
  });

  // Trigger the initial load.
  container.read(productListProvider);

  // Wait for the async operation.
  await container.read(productListProvider.future);

  expect(states, [
    isA<AsyncLoading<List<Product>>>(),
    isA<AsyncData<List<Product>>>(),
  ]);
});
```

---

## Common Patterns

### Pagination

```dart
@riverpod
class PaginatedArticles extends _$PaginatedArticles {
  int _page = 0;
  bool _hasMore = true;

  @override
  Future<List<Article>> build() async {
    _page = 0;
    _hasMore = true;
    return _fetchPage(0);
  }

  Future<List<Article>> _fetchPage(int page) async {
    final client = ref.read(apiClientProvider);
    final articles = await client.getArticles(page: page, pageSize: 20);
    if (articles.length < 20) _hasMore = false;
    return articles;
  }

  Future<void> loadNextPage() async {
    if (!_hasMore) return;
    final current = state.valueOrNull ?? [];
    _page++;
    final next = await _fetchPage(_page);
    state = AsyncData([...current, ...next]);
  }
}
```

### Search-as-you-type with debounce

```dart
@riverpod
class SearchQuery extends _$SearchQuery {
  Timer? _debounce;

  @override
  String build() => '';

  void update(String value) {
    _debounce?.cancel();
    _debounce = Timer(const Duration(milliseconds: 400), () {
      state = value;
    });
    ref.onDispose(() => _debounce?.cancel());
  }
}

@riverpod
Future<List<SearchResult>> searchResults(Ref ref) async {
  final query = ref.watch(searchQueryProvider);
  if (query.isEmpty) return [];
  final client = ref.watch(apiClientProvider);
  return client.search(query);
}
```

### Auth state with redirect

```dart
@Riverpod(keepAlive: true)
class Auth extends _$Auth {
  @override
  Future<User?> build() async {
    final repo = ref.watch(authRepositoryProvider);
    return repo.currentUser;
  }

  Future<void> signIn(String email, String password) async {
    state = const AsyncLoading();
    state = await AsyncValue.guard(
      () => ref.read(authRepositoryProvider).signIn(email, password),
    );
  }

  Future<void> signOut() async {
    await ref.read(authRepositoryProvider).signOut();
    ref.invalidateSelf();
  }

  bool get isAuthenticated => state.valueOrNull != null;
}

// In GoRouter redirect:
GoRouter routerConfig(Ref ref) {
  final auth = ref.watch(authProvider);
  return GoRouter(
    redirect: (context, routerState) {
      final isLoggedIn = auth.valueOrNull != null;
      final isOnLogin = routerState.matchedLocation == '/login';

      if (!isLoggedIn && !isOnLogin) return '/login';
      if (isLoggedIn && isOnLogin) return '/';
      return null;
    },
    routes: [
      GoRoute(path: '/', builder: (_, __) => const HomePage()),
      GoRoute(path: '/login', builder: (_, __) => const LoginPage()),
    ],
  );
}
```

### Combining providers (derived state)

```dart
@riverpod
List<Todo> filteredTodos(Ref ref) {
  final todos = ref.watch(todoListProvider);
  final filter = ref.watch(todoFilterProvider);

  return switch (filter) {
    TodoFilter.all => todos,
    TodoFilter.active => todos.where((t) => !t.done).toList(),
    TodoFilter.completed => todos.where((t) => t.done).toList(),
  };
}

@riverpod
TodoStats todoStats(Ref ref) {
  final todos = ref.watch(todoListProvider);
  return TodoStats(
    total: todos.length,
    completed: todos.where((t) => t.done).length,
    active: todos.where((t) => !t.done).length,
  );
}
```
