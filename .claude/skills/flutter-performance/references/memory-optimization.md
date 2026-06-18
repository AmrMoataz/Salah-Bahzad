# Memory Optimization

## Memory Leak Detection with DevTools

Memory leaks in Flutter typically fall into three categories:

1. **Dart heap leaks:** Objects that should be garbage collected but are
   retained by forgotten references (listeners, subscriptions, closures).
2. **External memory leaks:** Native resources (images, platform channels)
   not released when their Dart wrapper is disposed.
3. **Growing caches:** Unbounded caches that accumulate entries over time.

### Detection Workflow

1. Open DevTools > **Memory** tab.
2. Take a **baseline heap snapshot** after the app reaches a stable state.
3. Navigate to the suspect screen several times (push and pop).
4. Take a **second snapshot**.
5. Use the **Diff** view to compare snapshots.
6. Filter for your app's class names -- any object count that grows with each
   navigation is a candidate leak.

### Leak Detection in Tests

```dart
import 'package:flutter_test/flutter_test.dart';
import 'package:leak_tracker_flutter_testing/leak_tracker_flutter_testing.dart';

void main() {
  // Automatically detect leaks in widget tests
  testWidgets(
    'MyWidget does not leak',
    experimentalLeakTesting: LeakTesting.settings.withTracked(
      classes: ['MyController', 'MyService'],
    ),
    (tester) async {
      await tester.pumpWidget(const MaterialApp(home: MyWidget()));
      await tester.pumpAndSettle();

      // Navigate away, triggering dispose
      await tester.tap(find.byIcon(Icons.arrow_back));
      await tester.pumpAndSettle();

      // Leak tracker verifies disposal at end of test
    },
  );
}
```

## Disposing Controllers, Streams, and Subscriptions

Every resource acquired in `initState()` (or lazily) **must** be released in
`dispose()`. This is the single most common source of Flutter memory leaks.

### Complete Disposal Pattern

```dart
import 'dart:async';

import 'package:flutter/material.dart';

class ResourceOwnerWidget extends StatefulWidget {
  const ResourceOwnerWidget({super.key});

  @override
  State<ResourceOwnerWidget> createState() => _ResourceOwnerWidgetState();
}

class _ResourceOwnerWidgetState extends State<ResourceOwnerWidget>
    with SingleTickerProviderStateMixin {
  late final AnimationController _animationController;
  late final TextEditingController _textController;
  late final ScrollController _scrollController;
  late final FocusNode _focusNode;
  late final StreamSubscription<int> _tickSubscription;
  StreamSubscription<String>? _eventSubscription;

  @override
  void initState() {
    super.initState();

    _animationController = AnimationController(
      vsync: this,
      duration: const Duration(milliseconds: 300),
    );

    _textController = TextEditingController();
    _scrollController = ScrollController();
    _focusNode = FocusNode();

    _tickSubscription = Stream<int>.periodic(
      const Duration(seconds: 1),
      (i) => i,
    ).listen(_onTick);
  }

  @override
  void didChangeDependencies() {
    super.didChangeDependencies();
    // Safe to subscribe to inherited widget streams here
    // Always cancel previous subscription before creating new one
    _eventSubscription?.cancel();
    _eventSubscription = _createEventStream().listen(_onEvent);
  }

  void _onTick(int tick) {
    if (!mounted) return;
    // ... update state
  }

  void _onEvent(String event) {
    if (!mounted) return;
    // ... handle event
  }

  Stream<String> _createEventStream() async* {
    yield 'event';
  }

  @override
  void dispose() {
    // Dispose in reverse order of creation
    _eventSubscription?.cancel();
    _tickSubscription.cancel();
    _focusNode.dispose();
    _scrollController.dispose();
    _textController.dispose();
    _animationController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return const Placeholder();
  }
}
```

### Reusable Disposal Mixin

```dart
import 'dart:async';

import 'package:flutter/widgets.dart';

/// Tracks disposable resources and disposes them automatically.
mixin AutoDisposeMixin<T extends StatefulWidget> on State<T> {
  final List<StreamSubscription<dynamic>> _subscriptions = [];
  final List<ChangeNotifier> _notifiers = [];

  /// Track a stream subscription for automatic cancellation.
  StreamSubscription<S> autoCancel<S>(StreamSubscription<S> subscription) {
    _subscriptions.add(subscription);
    return subscription;
  }

  /// Track a ChangeNotifier for automatic disposal.
  N autoDispose<N extends ChangeNotifier>(N notifier) {
    _notifiers.add(notifier);
    return notifier;
  }

  @override
  void dispose() {
    for (final sub in _subscriptions) {
      sub.cancel();
    }
    for (final notifier in _notifiers) {
      notifier.dispose();
    }
    _subscriptions.clear();
    _notifiers.clear();
    super.dispose();
  }
}

// Usage:
class _MyWidgetState extends State<MyWidget> with AutoDisposeMixin<MyWidget> {
  late final TextEditingController _controller;

  @override
  void initState() {
    super.initState();
    _controller = autoDispose(TextEditingController());
    autoCancel(someStream.listen(_onData));
  }

  void _onData(dynamic data) {
    if (!mounted) return;
    // ...
  }

  @override
  Widget build(BuildContext context) => const Placeholder();
}
```

## Image Memory Management

Images are one of the largest memory consumers in Flutter apps. A
3000x4000 JPEG decodes to ~48 MB of raw pixel data in memory.

### Resize at Decode Time

Use `cacheWidth` and `cacheHeight` to decode images at the display size
rather than the source size:

```dart
// BEFORE: full-resolution decode (48 MB per image)
Image.network('https://example.com/photo.jpg')

// AFTER: decode at display size (~1 MB per image)
Image.network(
  'https://example.com/photo.jpg',
  cacheWidth: 300,  // pixels, not logical pixels
  cacheHeight: 400,
)
```

For `AssetImage` and `Image.asset`, the same parameters apply:

```dart
Image.asset(
  'assets/images/hero.jpg',
  cacheWidth: (MediaQuery.sizeOf(context).width *
      MediaQuery.devicePixelRatioOf(context))
      .toInt(),
)
```

### Image Cache Eviction

Flutter's default `ImageCache` holds 1000 images / 100 MB. For apps with
many images, tune the cache:

```dart
void main() {
  // Reduce image cache for memory-constrained devices
  PaintingBinding.instance.imageCache
    ..maximumSize = 200         // max 200 images
    ..maximumSizeBytes = 50 << 20; // max 50 MB

  runApp(const MyApp());
}
```

Evict specific images when navigating away from a screen:

```dart
void _onScreenDispose() {
  // Evict a specific image from the cache
  final provider = NetworkImage('https://example.com/large-photo.jpg');
  imageCache.evict(provider);
}
```

### Placeholder and Fade-In Pattern

Reduce perceived load time and prevent memory spikes from simultaneous
large-image decodes:

```dart
Widget buildThumbnail(String url) {
  return Image.network(
    url,
    cacheWidth: 200,
    cacheHeight: 200,
    fit: BoxFit.cover,
    frameBuilder: (context, child, frame, wasSynchronouslyLoaded) {
      if (wasSynchronouslyLoaded) return child;
      return AnimatedSwitcher(
        duration: const Duration(milliseconds: 200),
        child: frame != null
            ? child
            : Container(
                color: Colors.grey[300],
                child: const Center(
                  child: CircularProgressIndicator(strokeWidth: 2),
                ),
              ),
      );
    },
    errorBuilder: (context, error, stackTrace) {
      return Container(
        color: Colors.grey[300],
        child: const Icon(Icons.broken_image),
      );
    },
  );
}
```

## Large List Optimization

### ListView.builder (Lazy Construction)

`ListView.builder` only builds visible items plus a small buffer. Never use
`ListView(children: [...])` with more than ~20 items.

```dart
// BEFORE: all 10,000 items built at once
ListView(
  children: items.map((item) => ItemTile(item: item)).toList(),
)

// AFTER: only visible items built
ListView.builder(
  itemCount: items.length,
  itemBuilder: (context, index) => ItemTile(item: items[index]),
)
```

### itemExtent for Fixed-Height Items

When all items have the same height, `itemExtent` allows the framework to
skip measuring every child:

```dart
ListView.builder(
  itemCount: items.length,
  itemExtent: 72.0, // exact height of each tile
  itemBuilder: (context, index) => ItemTile(item: items[index]),
)
```

Alternative: `prototypeItem` lets Flutter measure one item and assume all
are the same size:

```dart
ListView.builder(
  itemCount: items.length,
  prototypeItem: const ItemTile(item: Item.placeholder),
  itemBuilder: (context, index) => ItemTile(item: items[index]),
)
```

### Slivers for Complex Scrolling

For mixed-content scrollable areas, use slivers to maintain lazy construction:

```dart
CustomScrollView(
  slivers: [
    const SliverAppBar(
      title: Text('Products'),
      floating: true,
    ),
    SliverList.builder(
      itemCount: categories.length,
      itemBuilder: (context, index) => CategoryHeader(
        category: categories[index],
      ),
    ),
    SliverGrid.builder(
      gridDelegate: const SliverGridDelegateWithFixedCrossAxisCount(
        crossAxisCount: 2,
        mainAxisSpacing: 8,
        crossAxisSpacing: 8,
      ),
      itemCount: products.length,
      itemBuilder: (context, index) => ProductCard(
        product: products[index],
      ),
    ),
  ],
)
```

### Keep-Alive for Expensive Items

Prevent disposal of items that are expensive to rebuild when they scroll
offscreen temporarily:

```dart
class ExpensiveListItem extends StatefulWidget {
  const ExpensiveListItem({super.key, required this.data});

  final ItemData data;

  @override
  State<ExpensiveListItem> createState() => _ExpensiveListItemState();
}

class _ExpensiveListItemState extends State<ExpensiveListItem>
    with AutomaticKeepAliveClientMixin {
  @override
  bool get wantKeepAlive => true;

  @override
  Widget build(BuildContext context) {
    super.build(context); // required by AutomaticKeepAliveClientMixin
    return Card(
      child: Text(widget.data.title),
    );
  }
}
```

> **Warning:** Use `AutomaticKeepAliveClientMixin` sparingly. Keeping too
> many items alive defeats the purpose of lazy list building.

## Isolate.run for Heavy Computation

The main isolate shares the UI thread. Any computation exceeding ~4 ms risks
causing jank. Move heavy work to a background isolate.

### Isolate.run (Simple One-Shot)

`Isolate.run` spawns an isolate, runs a function, and returns the result.
The isolate is discarded afterward.

```dart
import 'dart:convert';
import 'dart:isolate';

class ProductApi {
  Future<List<Product>> fetchProducts(Uri url) async {
    final response = await _httpClient.get(url);

    // Parse JSON on a background isolate
    final products = await Isolate.run(() {
      final json = jsonDecode(response.body) as List<dynamic>;
      return json
          .cast<Map<String, dynamic>>()
          .map(Product.fromJson)
          .toList();
    });

    return products;
  }
}
```

> **Important:** The function passed to `Isolate.run` must be a top-level
> function or a static method. Closures that capture `this` or local state
> will fail because isolates do not share memory.

### Isolate.run with Records (Dart 3+)

Use records to pass multiple parameters and return structured results:

```dart
import 'dart:isolate';
import 'dart:typed_data';

typedef ImageProcessResult = ({
  Uint8List thumbnail,
  int width,
  int height,
  String dominantColor,
});

Future<ImageProcessResult> processImage(Uint8List rawBytes) async {
  return Isolate.run(() {
    // Heavy image processing
    final decoded = _decodeImage(rawBytes);
    final thumbnail = _generateThumbnail(decoded, maxWidth: 300);
    final color = _extractDominantColor(decoded);

    return (
      thumbnail: thumbnail,
      width: decoded.width,
      height: decoded.height,
      dominantColor: color,
    );
  });
}
```

## compute() Function

`compute()` is a convenience wrapper around `Isolate.run` that has been
available since before Dart 3. In modern Dart, **prefer `Isolate.run`** as
it supports more flexible return types and has clearer semantics.

```dart
import 'package:flutter/foundation.dart';

// compute() requires a top-level or static function with a single argument
Future<List<Product>> parseProductsInBackground(String jsonBody) {
  return compute(_parseProducts, jsonBody);
}

List<Product> _parseProducts(String jsonBody) {
  final json = jsonDecode(jsonBody) as List<dynamic>;
  return json
      .cast<Map<String, dynamic>>()
      .map(Product.fromJson)
      .toList();
}
```

### When to Use Isolates vs compute()

| Scenario | Recommendation |
|---|---|
| One-shot computation (JSON parsing, image processing) | `Isolate.run` |
| Legacy code using `compute()` | Keep using `compute()` until migration |
| Long-running background worker | Spawn a persistent isolate with `Isolate.spawn` and communicate via `SendPort`/`ReceivePort` |
| Multiple sequential tasks | Persistent isolate to avoid spawn overhead |

### Persistent Isolate Worker

```dart
import 'dart:async';
import 'dart:isolate';

/// A long-lived background worker backed by a persistent isolate.
class BackgroundWorker {
  BackgroundWorker._({
    required SendPort sendPort,
    required Isolate isolate,
  })  : _sendPort = sendPort,
        _isolate = isolate;

  final SendPort _sendPort;
  final Isolate _isolate;
  final Map<int, Completer<dynamic>> _pending = {};
  int _nextId = 0;

  /// Spawn the worker isolate.
  static Future<BackgroundWorker> spawn() async {
    final receivePort = ReceivePort();
    final isolate = await Isolate.spawn(
      _workerEntryPoint,
      receivePort.sendPort,
    );

    final completer = Completer<SendPort>();

    late final StreamSubscription<dynamic> subscription;
    subscription = receivePort.listen((message) {
      if (message is SendPort) {
        completer.complete(message);
      }
    });

    final workerSendPort = await completer.future;
    await subscription.cancel();

    final worker = BackgroundWorker._(
      sendPort: workerSendPort,
      isolate: isolate,
    );

    // Listen for responses
    final responsePort = ReceivePort();
    responsePort.listen(worker._handleResponse);
    worker._sendPort.send(('setResponsePort', responsePort.sendPort));

    return worker;
  }

  static void _workerEntryPoint(SendPort mainSendPort) {
    final receivePort = ReceivePort();
    mainSendPort.send(receivePort.sendPort);

    SendPort? responsePort;

    receivePort.listen((message) {
      if (message case ('setResponsePort', SendPort port)) {
        responsePort = port;
        return;
      }

      if (message case (int id, String task, dynamic payload)) {
        final result = _executeTask(task, payload);
        responsePort?.send((id, result));
      }
    });
  }

  static dynamic _executeTask(String task, dynamic payload) {
    return switch (task) {
      'parseJson' => jsonDecode(payload as String),
      'compress' => _compress(payload as List<int>),
      _ => throw ArgumentError('Unknown task: $task'),
    };
  }

  static List<int> _compress(List<int> data) {
    // Placeholder for actual compression logic
    return data;
  }

  /// Send a task to the worker and await the result.
  Future<T> execute<T>(String task, dynamic payload) {
    final id = _nextId++;
    final completer = Completer<T>();
    _pending[id] = completer;
    _sendPort.send((id, task, payload));
    return completer.future;
  }

  void _handleResponse(dynamic message) {
    if (message case (int id, dynamic result)) {
      _pending.remove(id)?.complete(result);
    }
  }

  /// Shut down the worker isolate.
  void dispose() {
    _isolate.kill(priority: Isolate.beforeNextEvent);
    for (final completer in _pending.values) {
      completer.completeError(
        StateError('Worker disposed before completing'),
      );
    }
    _pending.clear();
  }
}
```

## Weak References

Use `WeakReference` to hold a reference to an object without preventing
garbage collection. Useful for caches that should not pin objects in memory:

```dart
class SoftCache<K, V extends Object> {
  final Map<K, WeakReference<V>> _cache = {};

  V? get(K key) => _cache[key]?.target;

  void put(K key, V value) {
    _cache[key] = WeakReference(value);
  }

  /// Periodically prune entries whose targets have been collected.
  void prune() {
    _cache.removeWhere((_, ref) => ref.target == null);
  }
}

// Usage:
final cache = SoftCache<String, ProductDetail>();
cache.put('product-123', detail);

// Later -- may return null if GC collected the value
final cached = cache.get('product-123');
```

### Finalizer for Cleanup

Combine `WeakReference` with `Finalizer` to run cleanup logic when an
object is collected:

```dart
class NativeResourceManager {
  static final Finalizer<int> _finalizer = Finalizer((handle) {
    // Release the native resource identified by handle
    _releaseNativeHandle(handle);
  });

  static void _releaseNativeHandle(int handle) {
    // Platform channel call to release native memory
    debugPrint('Released native handle: $handle');
  }

  /// Register a Dart object with its native handle.
  /// When the Dart object is GC'd, the native handle is released.
  void track(Object dartObject, int nativeHandle) {
    _finalizer.attach(dartObject, nativeHandle);
  }
}
```

## Object Pooling Patterns

For objects that are expensive to create and are needed frequently (e.g.,
paint objects in custom painters, reusable buffers):

```dart
import 'dart:collection';

class ObjectPool<T> {
  ObjectPool({
    required T Function() create,
    required void Function(T) reset,
    int maxSize = 32,
  })  : _create = create,
        _reset = reset,
        _maxSize = maxSize;

  final T Function() _create;
  final void Function(T) _reset;
  final int _maxSize;
  final Queue<T> _available = Queue<T>();

  int get availableCount => _available.length;

  /// Acquire an object from the pool, creating one if none are available.
  T acquire() {
    if (_available.isNotEmpty) {
      return _available.removeFirst();
    }
    return _create();
  }

  /// Return an object to the pool after use.
  void release(T object) {
    if (_available.length < _maxSize) {
      _reset(object);
      _available.addLast(object);
    }
    // If the pool is full, the object is simply discarded for GC.
  }

  /// Discard all pooled objects.
  void clear() {
    _available.clear();
  }
}

// Example: pooling Paint objects in a CustomPainter
class ParticleSystemPainter extends CustomPainter {
  ParticleSystemPainter({required this.particles});

  final List<Particle> particles;

  static final _paintPool = ObjectPool<Paint>(
    create: Paint.new,
    reset: (paint) {
      paint
        ..color = const Color(0xFF000000)
        ..style = PaintingStyle.fill
        ..strokeWidth = 0.0;
    },
  );

  @override
  void paint(Canvas canvas, Size size) {
    for (final particle in particles) {
      final paint = _paintPool.acquire();
      paint
        ..color = particle.color
        ..style = PaintingStyle.fill;

      canvas.drawCircle(particle.position, particle.radius, paint);

      _paintPool.release(paint);
    }
  }

  @override
  bool shouldRepaint(ParticleSystemPainter oldDelegate) => true;
}

class Particle {
  Particle({
    required this.position,
    required this.radius,
    required this.color,
  });

  final Offset position;
  final double radius;
  final Color color;
}
```

## Avoiding Closures That Capture Large Objects

Closures implicitly capture variables from their enclosing scope. If a
closure captures a reference to a large object (a bitmap, a large list, a
widget tree), that object cannot be garbage collected until the closure
itself is released.

```dart
// BAD: closure captures `largeImageBytes` and keeps it alive
// for the entire duration of the delayed callback
class _BadExampleState extends State<BadExample> {
  void processImage() {
    final largeImageBytes = _loadFullResolutionImage(); // 50 MB

    final thumbnail = _generateThumbnail(largeImageBytes);

    // This closure captures largeImageBytes even though it only needs thumbnail
    Future.delayed(const Duration(seconds: 5), () {
      _uploadThumbnail(thumbnail);
      debugPrint('Original size was ${largeImageBytes.length}');
    });
    // largeImageBytes is pinned in memory for 5 seconds
  }

  // ...
}

// GOOD: extract needed values before creating the closure
class _GoodExampleState extends State<GoodExample> {
  void processImage() {
    final largeImageBytes = _loadFullResolutionImage(); // 50 MB

    final thumbnail = _generateThumbnail(largeImageBytes);
    final originalSize = largeImageBytes.length;
    // largeImageBytes is now eligible for GC

    Future.delayed(const Duration(seconds: 5), () {
      _uploadThumbnail(thumbnail);
      debugPrint('Original size was $originalSize');
    });
  }

  // ...
}
```

### Closure Capture Checklist

| Risk | Mitigation |
|---|---|
| Timer callbacks capturing `this` or large state | Extract needed values into local variables first |
| Stream `.listen()` callbacks | Cancel subscription in `dispose()`; check `mounted` |
| `Future.then()` chains | Use `async`/`await` for clearer scope; null out references when done |
| `ChangeNotifier.addListener()` closures | Always `removeListener()` in `dispose()` |
| Global singletons holding widget references | Use `WeakReference` or event-based patterns instead |
