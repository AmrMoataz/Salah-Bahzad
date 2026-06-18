# Widget Lifecycle

## StatelessWidget vs StatefulWidget Decision

Choose **StatelessWidget** when:
- The widget depends only on its constructor arguments and inherited data.
- No mutable local state is needed.
- No subscriptions, controllers, or animations are involved.

Choose **StatefulWidget** when:
- The widget owns mutable state that changes over its lifetime.
- You need lifecycle hooks (`initState`, `dispose`) to manage resources.
- You need to react to widget configuration changes via `didUpdateWidget`.

```dart
// StatelessWidget -- pure function of its inputs
class PriceTag extends StatelessWidget {
  const PriceTag({super.key, required this.amount, this.currency = 'USD'});

  final double amount;
  final String currency;

  @override
  Widget build(BuildContext context) {
    final formatted = amount.toStringAsFixed(2);
    return Text('$currency $formatted', style: const TextStyle(fontWeight: FontWeight.bold));
  }
}
```

---

## Full StatefulWidget Lifecycle

The lifecycle proceeds in order:

1. **`createState()`** -- Called once. Creates the mutable `State` object.
2. **`initState()`** -- Called once after `createState`. Initialize controllers, subscriptions, and one-time setup here.
3. **`didChangeDependencies()`** -- Called after `initState` and whenever an `InheritedWidget` ancestor changes. Use this for work that depends on `BuildContext` (e.g., reading `Theme.of(context)`).
4. **`build()`** -- Called whenever the framework needs to render. Must be pure and fast.
5. **`didUpdateWidget(covariant OldWidget oldWidget)`** -- Called when the parent rebuilds with a new widget of the same `runtimeType`. Compare `oldWidget` to the current widget and update state accordingly.
6. **`deactivate()`** -- Called when the `State` is removed from the tree. May be reinserted later (e.g., via `GlobalKey`).
7. **`dispose()`** -- Called once when the `State` is permanently removed. Cancel subscriptions, dispose controllers, and release resources here.

```dart
class CountdownTimer extends StatefulWidget {
  const CountdownTimer({super.key, required this.durationSeconds});

  final int durationSeconds;

  @override
  State<CountdownTimer> createState() => _CountdownTimerState();
}

class _CountdownTimerState extends State<CountdownTimer> {
  late int _remaining;
  Timer? _timer;

  // 1. initState -- start the timer
  @override
  void initState() {
    super.initState();
    _remaining = widget.durationSeconds;
    _startTimer();
  }

  // 2. didChangeDependencies -- react to inherited changes
  @override
  void didChangeDependencies() {
    super.didChangeDependencies();
    // Example: read locale from context if formatting depends on it
  }

  // 3. didUpdateWidget -- parent changed the duration
  @override
  void didUpdateWidget(covariant CountdownTimer oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (oldWidget.durationSeconds != widget.durationSeconds) {
      _timer?.cancel();
      _remaining = widget.durationSeconds;
      _startTimer();
    }
  }

  // 4. dispose -- clean up
  @override
  void dispose() {
    _timer?.cancel();
    super.dispose();
  }

  void _startTimer() {
    _timer = Timer.periodic(const Duration(seconds: 1), (_) {
      if (_remaining > 0) {
        setState(() => _remaining--);
      } else {
        _timer?.cancel();
      }
    });
  }

  @override
  Widget build(BuildContext context) {
    final minutes = (_remaining ~/ 60).toString().padLeft(2, '0');
    final seconds = (_remaining % 60).toString().padLeft(2, '0');
    return Text(
      '$minutes:$seconds',
      style: Theme.of(context).textTheme.headlineMedium,
    );
  }
}
```

---

## When to Use Each Hook

| Hook | Use Case |
|---|---|
| `initState` | Create controllers, start timers, set initial values derived from `widget` |
| `didChangeDependencies` | Read `InheritedWidget` data (theme, locale, media query) that may change |
| `build` | Return the widget tree. No side effects. |
| `didUpdateWidget` | Respond to parent passing new configuration without a full teardown |
| `deactivate` | Rarely needed. Unregister from parent data structures that track children. |
| `dispose` | Cancel timers, close streams, dispose `TextEditingController`, `AnimationController`, `FocusNode` |

---

## HookWidget from flutter_hooks

The `flutter_hooks` package eliminates boilerplate by letting you use React-style hooks inside a widget. Every hook automatically handles disposal.

### useState

Holds a single piece of mutable state. Equivalent to a field + `setState`.

```dart
class LikeButton extends HookWidget {
  const LikeButton({super.key});

  @override
  Widget build(BuildContext context) {
    final isLiked = useState(false);

    return IconButton(
      icon: Icon(isLiked.value ? Icons.favorite : Icons.favorite_border),
      color: isLiked.value ? Colors.red : Colors.grey,
      onPressed: () => isLiked.value = !isLiked.value,
    );
  }
}
```

### useEffect

Runs a side-effect function. Returns an optional dispose callback. Reacts to a list of dependency keys.

```dart
class UserProfile extends HookWidget {
  const UserProfile({super.key, required this.userId});

  final String userId;

  @override
  Widget build(BuildContext context) {
    final user = useState<User?>(null);
    final isLoading = useState(true);

    useEffect(() {
      isLoading.value = true;
      final subscription = UserRepository.instance
          .streamUser(userId)
          .listen((data) {
        user.value = data;
        isLoading.value = false;
      });

      // Dispose callback -- cancels subscription when userId changes or widget disposes
      return subscription.cancel;
    }, [userId]);

    if (isLoading.value) {
      return const CircularProgressIndicator();
    }

    return Text(user.value?.displayName ?? 'Unknown');
  }
}
```

### useMemoized

Caches an expensive computation. Only recomputes when the keys list changes.

```dart
class ExpensiveChart extends HookWidget {
  const ExpensiveChart({super.key, required this.rawData});

  final List<double> rawData;

  @override
  Widget build(BuildContext context) {
    // Only recomputes when rawData reference changes
    final processed = useMemoized(() => _computeChartPoints(rawData), [rawData]);

    return CustomPaint(
      painter: ChartPainter(points: processed),
      size: const Size(double.infinity, 200),
    );
  }

  static List<Offset> _computeChartPoints(List<double> data) {
    return data.indexed.map((e) => Offset(e.$1.toDouble(), e.$2)).toList();
  }
}
```

### useTextEditingController

Creates and disposes a `TextEditingController` automatically.

```dart
class SearchBar extends HookWidget {
  const SearchBar({super.key, required this.onSearch});

  final ValueChanged<String> onSearch;

  @override
  Widget build(BuildContext context) {
    final controller = useTextEditingController();

    return TextField(
      controller: controller,
      decoration: const InputDecoration(
        hintText: 'Search...',
        prefixIcon: Icon(Icons.search),
      ),
      onSubmitted: onSearch,
    );
  }
}
```

### useAnimationController

Creates an `AnimationController` with automatic disposal and vsync via a built-in ticker.

```dart
class PulseIcon extends HookWidget {
  const PulseIcon({super.key});

  @override
  Widget build(BuildContext context) {
    final controller = useAnimationController(
      duration: const Duration(milliseconds: 800),
    )..repeat(reverse: true);

    return ScaleTransition(
      scale: Tween<double>(begin: 0.8, end: 1.2).animate(
        CurvedAnimation(parent: controller, curve: Curves.easeInOut),
      ),
      child: const Icon(Icons.notifications, size: 32),
    );
  }
}
```

---

## ConsumerWidget and ConsumerStatefulWidget (Riverpod)

When using Riverpod for state management, use `ConsumerWidget` or `ConsumerStatefulWidget` to access providers.

### ConsumerWidget (Stateless equivalent)

```dart
class ProductCard extends ConsumerWidget {
  const ProductCard({super.key, required this.productId});

  final String productId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final product = ref.watch(productProvider(productId));

    return switch (product) {
      AsyncData(:final value) => Card(
          child: ListTile(
            title: Text(value.name),
            subtitle: Text('\$${value.price.toStringAsFixed(2)}'),
            trailing: IconButton(
              icon: const Icon(Icons.add_shopping_cart),
              onPressed: () => ref.read(cartProvider.notifier).add(value),
            ),
          ),
        ),
      AsyncError(:final error) => Text('Error: $error'),
      _ => const CircularProgressIndicator(),
    };
  }
}
```

### ConsumerStatefulWidget (Stateful equivalent)

Use this when you need both Riverpod access and lifecycle hooks.

```dart
class ChatRoom extends ConsumerStatefulWidget {
  const ChatRoom({super.key, required this.roomId});

  final String roomId;

  @override
  ConsumerState<ChatRoom> createState() => _ChatRoomState();
}

class _ChatRoomState extends ConsumerState<ChatRoom> {
  late final TextEditingController _messageController;
  late final ScrollController _scrollController;

  @override
  void initState() {
    super.initState();
    _messageController = TextEditingController();
    _scrollController = ScrollController();
  }

  @override
  void dispose() {
    _messageController.dispose();
    _scrollController.dispose();
    super.dispose();
  }

  void _sendMessage() {
    final text = _messageController.text.trim();
    if (text.isEmpty) return;

    ref.read(chatProvider(widget.roomId).notifier).send(text);
    _messageController.clear();
  }

  @override
  Widget build(BuildContext context) {
    final messages = ref.watch(chatProvider(widget.roomId));

    return Column(
      children: [
        Expanded(
          child: switch (messages) {
            AsyncData(:final value) => ListView.builder(
                controller: _scrollController,
                reverse: true,
                itemCount: value.length,
                itemBuilder: (context, index) => MessageBubble(
                  key: ValueKey(value[index].id),
                  message: value[index],
                ),
              ),
            AsyncError(:final error) => Center(child: Text('Error: $error')),
            _ => const Center(child: CircularProgressIndicator()),
          },
        ),
        Padding(
          padding: const EdgeInsets.all(8.0),
          child: Row(
            children: [
              Expanded(
                child: TextField(
                  controller: _messageController,
                  decoration: const InputDecoration(hintText: 'Type a message...'),
                  onSubmitted: (_) => _sendMessage(),
                ),
              ),
              IconButton(
                icon: const Icon(Icons.send),
                onPressed: _sendMessage,
              ),
            ],
          ),
        ),
      ],
    );
  }
}
```

### HookConsumerWidget (Hooks + Riverpod)

Combines hooks with Riverpod access when you need both.

```dart
class AnimatedProductPrice extends HookConsumerWidget {
  const AnimatedProductPrice({super.key, required this.productId});

  final String productId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final product = ref.watch(productProvider(productId));
    final animationController = useAnimationController(
      duration: const Duration(milliseconds: 300),
    );

    useEffect(() {
      animationController.forward(from: 0);
      return null;
    }, [product]);

    return switch (product) {
      AsyncData(:final value) => FadeTransition(
          opacity: animationController,
          child: Text(
            '\$${value.price.toStringAsFixed(2)}',
            style: Theme.of(context).textTheme.headlineSmall,
          ),
        ),
      _ => const SizedBox.shrink(),
    };
  }
}
```
