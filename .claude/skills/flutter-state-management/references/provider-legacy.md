# Provider & ValueNotifier Reference Guide

## Overview

The `provider` package was the first community-recommended state management
solution for Flutter and is still widely used in production codebases. For new
projects, Riverpod (by the same author) is preferred, but Provider remains
appropriate for maintenance work and simple use cases. This guide also covers
`ValueNotifier`, a built-in Flutter class for lightweight reactive state that
requires no external dependencies.

---

## Table of Contents

1. [ValueNotifier Basics](#valuenotifier-basics)
2. [ChangeNotifier](#changenotifier)
3. [Provider Package](#provider-package)
4. [When Provider Is Still Appropriate](#when-provider-is-still-appropriate)
5. [Migration Path: Provider to Riverpod](#migration-path-provider-to-riverpod)

---

## ValueNotifier Basics

`ValueNotifier<T>` is a `ChangeNotifier` that holds a single value and notifies
listeners whenever that value changes. It is part of the Flutter framework --
zero dependencies.

### Simple counter

```dart
class CounterPage extends StatefulWidget {
  const CounterPage({super.key});

  @override
  State<CounterPage> createState() => _CounterPageState();
}

class _CounterPageState extends State<CounterPage> {
  final _counter = ValueNotifier<int>(0);

  @override
  void dispose() {
    _counter.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Counter')),
      body: Center(
        child: ValueListenableBuilder<int>(
          valueListenable: _counter,
          builder: (context, count, child) {
            return Text(
              '$count',
              style: Theme.of(context).textTheme.headlineMedium,
            );
          },
        ),
      ),
      floatingActionButton: FloatingActionButton(
        onPressed: () => _counter.value++,
        child: const Icon(Icons.add),
      ),
    );
  }
}
```

### Custom ValueNotifier with logic

```dart
class ToggleNotifier extends ValueNotifier<bool> {
  ToggleNotifier() : super(false);

  void toggle() => value = !value;
  void setOn() => value = true;
  void setOff() => value = false;
}
```

### Combining multiple ValueNotifiers

Use `Listenable.merge` to react to changes in several notifiers at once:

```dart
class FilterBar extends StatefulWidget {
  const FilterBar({super.key});

  @override
  State<FilterBar> createState() => _FilterBarState();
}

class _FilterBarState extends State<FilterBar> {
  final _search = ValueNotifier<String>('');
  final _sortAscending = ValueNotifier<bool>(true);
  final _selectedCategory = ValueNotifier<String?>(null);

  late final Listenable _combined;

  @override
  void initState() {
    super.initState();
    _combined = Listenable.merge([_search, _sortAscending, _selectedCategory]);
  }

  @override
  void dispose() {
    _search.dispose();
    _sortAscending.dispose();
    _selectedCategory.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return ListenableBuilder(
      listenable: _combined,
      builder: (context, child) {
        final filtered = _applyFilters(
          search: _search.value,
          ascending: _sortAscending.value,
          category: _selectedCategory.value,
        );
        return ProductGrid(products: filtered);
      },
    );
  }

  List<Product> _applyFilters({
    required String search,
    required bool ascending,
    required String? category,
  }) {
    var results = allProducts.where((p) {
      final matchesSearch =
          search.isEmpty || p.name.toLowerCase().contains(search.toLowerCase());
      final matchesCategory = category == null || p.category == category;
      return matchesSearch && matchesCategory;
    }).toList();

    results.sort((a, b) =>
        ascending ? a.name.compareTo(b.name) : b.name.compareTo(a.name));

    return results;
  }
}
```

### When to use ValueNotifier

- State is local to a single widget or a small subtree.
- The state is a single value (or a small immutable object).
- No external package dependencies are desired.
- Performance is critical and you need fine-grained rebuild control.

---

## ChangeNotifier

`ChangeNotifier` is the base class for objects that maintain a list of listeners.
It is more flexible than `ValueNotifier` because you can hold multiple fields
and call `notifyListeners()` manually.

```dart
class CartModel extends ChangeNotifier {
  final List<CartItem> _items = [];

  List<CartItem> get items => List.unmodifiable(_items);
  int get totalItems => _items.fold(0, (sum, item) => sum + item.quantity);
  double get totalPrice =>
      _items.fold(0.0, (sum, item) => sum + item.price * item.quantity);

  void addItem(Product product, {int quantity = 1}) {
    final index = _items.indexWhere((item) => item.productId == product.id);
    if (index >= 0) {
      _items[index] = _items[index].copyWith(
        quantity: _items[index].quantity + quantity,
      );
    } else {
      _items.add(CartItem(
        productId: product.id,
        name: product.name,
        price: product.price,
        quantity: quantity,
      ));
    }
    notifyListeners();
  }

  void removeItem(String productId) {
    _items.removeWhere((item) => item.productId == productId);
    notifyListeners();
  }

  void updateQuantity(String productId, int quantity) {
    final index = _items.indexWhere((item) => item.productId == productId);
    if (index >= 0) {
      if (quantity <= 0) {
        _items.removeAt(index);
      } else {
        _items[index] = _items[index].copyWith(quantity: quantity);
      }
      notifyListeners();
    }
  }

  void clear() {
    _items.clear();
    notifyListeners();
  }
}
```

**Downsides of ChangeNotifier:**

- It is mutable, making it harder to reason about state flow.
- `notifyListeners()` rebuilds all listeners, even if only one field changed.
- Testing requires manually listening for changes and asserting state.

---

## Provider Package

### Setup

```yaml
dependencies:
  provider: ^6.1.0
```

### ChangeNotifierProvider

The most common pattern: expose a `ChangeNotifier` via the widget tree.

```dart
class App extends StatelessWidget {
  const App({super.key});

  @override
  Widget build(BuildContext context) {
    return MultiProvider(
      providers: [
        ChangeNotifierProvider(create: (_) => CartModel()),
        ChangeNotifierProvider(create: (_) => ThemeModel()),
        Provider(create: (_) => ApiClient()),
      ],
      child: const MaterialApp(home: HomePage()),
    );
  }
}
```

### Reading and watching

```dart
class CartIcon extends StatelessWidget {
  const CartIcon({super.key});

  @override
  Widget build(BuildContext context) {
    // Rebuilds when CartModel calls notifyListeners().
    final itemCount = context.watch<CartModel>().totalItems;

    return Badge(
      label: Text('$itemCount'),
      child: const Icon(Icons.shopping_cart),
    );
  }
}

class AddToCartButton extends StatelessWidget {
  const AddToCartButton({super.key, required this.product});

  final Product product;

  @override
  Widget build(BuildContext context) {
    return ElevatedButton(
      onPressed: () {
        // Read once, do not subscribe.
        context.read<CartModel>().addItem(product);
      },
      child: const Text('Add to Cart'),
    );
  }
}
```

### Selector (selective rebuilds)

```dart
class CartTotal extends StatelessWidget {
  const CartTotal({super.key});

  @override
  Widget build(BuildContext context) {
    final total = context.select<CartModel, double>((cart) => cart.totalPrice);
    return Text('\$${total.toStringAsFixed(2)}');
  }
}
```

### FutureProvider

```dart
final settingsProvider = FutureProvider<AppSettings>((context) async {
  final prefs = await SharedPreferences.getInstance();
  return AppSettings.fromPrefs(prefs);
});
```

> Provider's `FutureProvider` is not auto-disposing and does not support
> invalidation. Prefer Riverpod's `FutureProvider` for new code.

### StreamProvider

```dart
class App extends StatelessWidget {
  const App({super.key});

  @override
  Widget build(BuildContext context) {
    return StreamProvider<ConnectivityStatus>(
      create: (_) => ConnectivityService().statusStream,
      initialData: ConnectivityStatus.online,
      child: const MaterialApp(home: HomePage()),
    );
  }
}
```

### ProxyProvider (derived state)

```dart
MultiProvider(
  providers: [
    Provider(create: (_) => ApiClient()),
    ChangeNotifierProvider(create: (_) => AuthModel()),
    ProxyProvider2<ApiClient, AuthModel, OrderRepository>(
      update: (_, apiClient, authModel, __) => OrderRepository(
        apiClient: apiClient,
        token: authModel.token,
      ),
    ),
  ],
  child: const App(),
)
```

### Testing with Provider

```dart
void main() {
  testWidgets('CartIcon shows badge count', (tester) async {
    final cart = CartModel()
      ..addItem(const Product(id: '1', name: 'A', price: 10))
      ..addItem(const Product(id: '2', name: 'B', price: 20));

    await tester.pumpWidget(
      ChangeNotifierProvider.value(
        value: cart,
        child: const MaterialApp(home: CartIcon()),
      ),
    );

    expect(find.text('2'), findsOneWidget);
  });
}
```

---

## When Provider Is Still Appropriate

| Scenario                                    | Recommendation       |
| ------------------------------------------- | -------------------- |
| Existing large codebase fully on Provider    | Maintain with Provider, migrate incrementally |
| Small app with 1-3 shared models            | Provider is fine     |
| Quick prototype or tutorial                 | Provider is fine     |
| New production project                      | Use Riverpod instead |
| Need auto-dispose, families, or overrides   | Use Riverpod instead |
| Complex dependency graph                    | Use Riverpod instead |

---

## Migration Path: Provider to Riverpod

### Step 1: Add Riverpod alongside Provider

```yaml
dependencies:
  provider: ^6.1.0
  flutter_riverpod: ^2.5.0
  riverpod_annotation: ^2.3.0
```

Wrap the app with both:

```dart
class App extends StatelessWidget {
  const App({super.key});

  @override
  Widget build(BuildContext context) {
    return ProviderScope(
      child: MultiProvider(
        providers: [
          ChangeNotifierProvider(create: (_) => LegacyCartModel()),
        ],
        child: const MaterialApp(home: HomePage()),
      ),
    );
  }
}
```

### Step 2: Create Riverpod equivalents for new features

```dart
// Old: ChangeNotifier
class LegacyCartModel extends ChangeNotifier {
  final List<CartItem> _items = [];
  List<CartItem> get items => List.unmodifiable(_items);
  void addItem(CartItem item) {
    _items.add(item);
    notifyListeners();
  }
}

// New: Riverpod Notifier
@riverpod
class Cart extends _$Cart {
  @override
  List<CartItem> build() => [];

  void addItem(CartItem item) {
    state = [...state, item];
  }

  void removeItem(String id) {
    state = state.where((item) => item.id != id).toList();
  }
}
```

### Step 3: Bridge Provider and Riverpod during migration

If a Riverpod provider needs data from a legacy Provider model, use a
`ChangeNotifierProvider` in Riverpod that wraps the existing model:

```dart
// Expose the legacy model as a Riverpod provider so new code can watch it.
final legacyCartProvider = ChangeNotifierProvider<LegacyCartModel>((ref) {
  // This instance should be the same one provided by the Provider package.
  // Consider moving the creation here and removing it from MultiProvider.
  return LegacyCartModel();
});
```

### Step 4: Migrate widgets one at a time

```dart
// Before (Provider)
class CartBadge extends StatelessWidget {
  const CartBadge({super.key});

  @override
  Widget build(BuildContext context) {
    final count = context.watch<LegacyCartModel>().items.length;
    return Badge(label: Text('$count'), child: const Icon(Icons.shopping_cart));
  }
}

// After (Riverpod)
class CartBadge extends ConsumerWidget {
  const CartBadge({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final count = ref.watch(cartProvider).length;
    return Badge(label: Text('$count'), child: const Icon(Icons.shopping_cart));
  }
}
```

### Step 5: Remove Provider when migration is complete

1. Remove all `context.watch`, `context.read`, and `context.select` calls.
2. Remove `MultiProvider` and all `ChangeNotifierProvider` instances.
3. Remove the `provider` package from `pubspec.yaml`.
4. Run `dart fix --apply` to clean up any remaining imports.

### Migration checklist

- [ ] Add `flutter_riverpod` and `ProviderScope` to the widget tree.
- [ ] For each `ChangeNotifier`, create an equivalent `Notifier` or
      `AsyncNotifier`.
- [ ] Update widgets from `StatelessWidget` / `context.watch` to
      `ConsumerWidget` / `ref.watch`.
- [ ] Replace `ProxyProvider` with Riverpod's provider composition
      (`ref.watch` inside a provider).
- [ ] Replace `FutureProvider` (provider pkg) with Riverpod's
      `FutureProvider` or `AsyncNotifierProvider`.
- [ ] Remove `MultiProvider` and the `provider` dependency.
- [ ] Run all tests and verify no regressions.
