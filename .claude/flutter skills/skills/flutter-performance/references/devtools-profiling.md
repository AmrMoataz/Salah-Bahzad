# DevTools Profiling

## Running in Profile Mode

**Never** measure performance in debug mode. Debug mode disables
optimizations, enables assertions, and runs significantly slower.

```bash
# Profile mode on a connected device
flutter run --profile

# Profile mode on a specific device
flutter run --profile -d <device-id>

# Profile mode with a specific entrypoint
flutter run --profile -t lib/main_production.dart
```

Profile mode:

- Compiles Dart to native code (AOT on mobile, JIT on desktop).
- Disables most debug assertions.
- Keeps the service protocol enabled for DevTools connection.
- Reflects real-world performance characteristics.

Launch DevTools from the terminal link printed by `flutter run`, or open it
manually:

```bash
dart devtools
```

## Performance View

### Frame Chart

The **Performance** tab in DevTools shows a frame chart with two rows:

- **UI thread (top row):** Dart framework work -- build, layout, paint.
- **Raster thread (bottom row):** GPU compositing and rasterization.

Each bar represents one frame. Color coding:

| Color | Meaning |
|---|---|
| Green | Frame completed within budget (< 16 ms for 60 fps) |
| Yellow | Frame slightly exceeded budget |
| Red | Frame significantly exceeded budget (jank) |

### Flame Chart

Click any frame bar to drill into its flame chart. The flame chart shows a
hierarchical breakdown of where time was spent:

```
Frame #142 (18.3 ms)
 +-- Build (4.2 ms)
 |    +-- MyHomePage.build (2.1 ms)
 |    +-- ProductList.build (1.8 ms)
 +-- Layout (3.1 ms)
 +-- Paint (2.4 ms)
 +-- Compositing (8.6 ms)   <-- bottleneck
```

In this example, compositing is the bottleneck. This often points to
excessive layers, save-layer operations, or expensive `BackdropFilter` usage.

### Reading the Timeline

Key events to watch for in the flame chart:

| Event | Indicates |
|---|---|
| `Animate` | Animation ticker callbacks |
| `Build` | Widget tree reconstruction |
| `Layout` | RenderObject layout pass |
| `Paint` | RenderObject painting |
| `Compositing` | Layer tree compositing for the GPU |
| `Semantics` | Accessibility tree construction |
| `Finalize tree` | Element cleanup and disposal |

## Identifying Jank

Jank occurs when a frame exceeds its budget:

- **60 fps target:** Each frame has 16.67 ms.
- **120 fps target:** Each frame has 8.33 ms.

### Systematic Jank Investigation

```dart
// Step 1: Add performance overlay to your app
MaterialApp(
  showPerformanceOverlay: true,
  home: const MyHomePage(),
);
```

```dart
// Step 2: Add timing instrumentation to suspect code paths
import 'dart:developer';

void expensiveOperation() {
  Timeline.startSync('expensiveOperation');
  try {
    // ... work ...
  } finally {
    Timeline.finishSync();
  }
}
```

```dart
// Step 3: Use SchedulerBinding to track frame timing programmatically
import 'package:flutter/scheduler.dart';

void setupFrameMonitoring() {
  SchedulerBinding.instance.addTimingsCallback((List<FrameTiming> timings) {
    for (final timing in timings) {
      final buildDuration = timing.buildDuration;
      final rasterDuration = timing.rasterDuration;
      final totalDuration = timing.totalSpan;

      if (totalDuration > const Duration(milliseconds: 16)) {
        debugPrint(
          'Jank detected: '
          'build=${buildDuration.inMilliseconds}ms '
          'raster=${rasterDuration.inMilliseconds}ms '
          'total=${totalDuration.inMilliseconds}ms',
        );
      }
    }
  });
}
```

## CPU Profiler

The CPU Profiler in DevTools captures a sampling profile of Dart code
execution. It provides three views:

### Top-Down View

Shows the call tree from root functions downward. Useful for understanding
which top-level call paths are most expensive.

### Bottom-Up View

Shows the most expensive leaf functions first, with their callers stacked
above. This is the **fastest way to find hotspots**:

```
Total: 1200 ms
  _JsonParser.parse        320 ms (26.7%)
  Image.resolve             210 ms (17.5%)
  RenderFlex.performLayout  180 ms (15.0%)
  ...
```

### Flame Chart View

A visual timeline showing function execution over time. Width represents
duration. Use this to find long-running synchronous operations blocking the
UI thread.

### Recording a CPU Profile

1. Open DevTools > **CPU Profiler** tab.
2. Click **Record**.
3. Perform the problematic interaction in the app.
4. Click **Stop**.
5. Analyze the results starting from the **Bottom-Up** view.

Filter by:

- **Dart only:** Hides native frames for cleaner analysis.
- **Package filter:** Focus on your app code vs framework code.

## Timeline Events

Custom timeline events help you correlate application logic with frame timing:

```dart
import 'dart:developer';

class ProductRepository {
  Future<List<Product>> fetchProducts() async {
    // This event appears in the DevTools timeline
    final flow = Flow.begin();
    Timeline.startSync('ProductRepository.fetchProducts', flow: flow);

    try {
      final response = await _httpClient.get(Uri.parse(_baseUrl));
      Timeline.startSync('JSON parsing');
      final products = _parseProducts(response.body);
      Timeline.finishSync(); // JSON parsing

      return products;
    } finally {
      Timeline.finishSync(); // fetchProducts
    }
  }

  List<Product> _parseProducts(String json) {
    // Heavy parsing -- consider moving to an isolate
    return (jsonDecode(json) as List<dynamic>)
        .cast<Map<String, dynamic>>()
        .map(Product.fromJson)
        .toList();
  }
}
```

## Rebuild Stats

### Tracking Widget Rebuilds

DevTools can count how many times each widget rebuilds per frame. Enable it
in the **Flutter Inspector** > **Track Widget Rebuilds**.

### Common Causes of Unnecessary Rebuilds

| Cause | Fix |
|---|---|
| Parent rebuilds, child has no `const` constructor | Add `const` where possible |
| `setState` at too high a level | Move state down to the smallest widget that needs it |
| `context.watch()` on large provider | Select only the fields needed with `context.select()` |
| `AnimatedBuilder` missing `child` parameter | Extract static subtrees into the `child` parameter |
| Inline closures creating new objects each build | Extract to final fields or use `const` callbacks |

### Example: Reducing Rebuilds with const and RepaintBoundary

```dart
// BEFORE: entire subtree rebuilds on every animation tick
class AnimatedCard extends StatelessWidget {
  const AnimatedCard({super.key, required this.animation});

  final Animation<double> animation;

  @override
  Widget build(BuildContext context) {
    return AnimatedBuilder(
      animation: animation,
      builder: (context, child) {
        return Transform.scale(
          scale: animation.value,
          child: Card(
            child: Column(
              children: [
                // This rebuilds every tick even though it never changes!
                const Icon(Icons.star, size: 48),
                const SizedBox(height: 8),
                Text(
                  'Rating',
                  style: Theme.of(context).textTheme.titleLarge,
                ),
                const SizedBox(height: 16),
                // This is the only part that should rebuild
                Text('Score: ${(animation.value * 100).toInt()}'),
              ],
            ),
          ),
        );
      },
    );
  }
}

// AFTER: static subtree extracted to `child` parameter
class AnimatedCard extends StatelessWidget {
  const AnimatedCard({super.key, required this.animation});

  final Animation<double> animation;

  @override
  Widget build(BuildContext context) {
    return AnimatedBuilder(
      animation: animation,
      // Static content passed as child -- not rebuilt on tick
      child: Card(
        child: Column(
          children: [
            const Icon(Icons.star, size: 48),
            const SizedBox(height: 8),
            Text(
              'Rating',
              style: Theme.of(context).textTheme.titleLarge,
            ),
          ],
        ),
      ),
      builder: (context, child) {
        return Transform.scale(
          scale: animation.value,
          child: Column(
            children: [
              child!, // reused, not rebuilt
              const SizedBox(height: 16),
              Text('Score: ${(animation.value * 100).toInt()}'),
            ],
          ),
        );
      },
    );
  }
}
```

### Finding Unnecessary Rebuilds Programmatically

```dart
import 'package:flutter/widgets.dart';

/// Mixin that logs when a widget rebuilds. Use during profiling only.
mixin RebuildTracker<T extends StatefulWidget> on State<T> {
  int _buildCount = 0;

  @override
  Widget build(BuildContext context) {
    _buildCount++;
    debugPrint('${widget.runtimeType} build #$_buildCount');
    return buildWithTracking(context);
  }

  Widget buildWithTracking(BuildContext context);
}

// Usage:
class _MyWidgetState extends State<MyWidget> with RebuildTracker<MyWidget> {
  @override
  Widget buildWithTracking(BuildContext context) {
    return const Text('Hello');
  }
}
```

## Memory Profiler

### Heap Snapshot

1. Open DevTools > **Memory** tab.
2. Click **Take Heap Snapshot**.
3. Filter by class name to find objects you expect to be garbage collected.
4. Compare two snapshots to find growth.

### Allocation Tracking

Enable allocation tracing to see where objects are allocated:

1. Click **Trace** in the Memory tab.
2. Perform the suspect interaction.
3. Click **Stop**.
4. Sort by allocation count or retained size.

### Key Metrics

| Metric | What It Means |
|---|---|
| **Used heap** | Memory currently occupied by live Dart objects |
| **External** | Memory allocated outside the Dart heap (images, platform channels) |
| **RSS** | Resident set size -- total memory the OS has allocated to the process |
| **GC events** | Garbage collection pauses; frequent full GCs indicate memory pressure |

### Example: Detecting a Leak

```dart
// Common leak: StreamSubscription not cancelled
class LeakyWidget extends StatefulWidget {
  const LeakyWidget({super.key});

  @override
  State<LeakyWidget> createState() => _LeakyWidgetState();
}

class _LeakyWidgetState extends State<LeakyWidget> {
  late final StreamSubscription<int> _subscription;

  @override
  void initState() {
    super.initState();
    // Leak: subscription holds a reference to this State
    _subscription = Stream.periodic(
      const Duration(seconds: 1),
      (i) => i,
    ).listen((value) {
      if (mounted) {
        setState(() {/* update */});
      }
    });
  }

  // FIX: Always cancel subscriptions in dispose()
  @override
  void dispose() {
    _subscription.cancel();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return const Placeholder();
  }
}
```

## Network Profiler

The **Network** tab records all HTTP traffic:

- Request/response headers and bodies.
- Timing breakdown (DNS, connect, TLS, first byte, download).
- Status codes and errors.

Use this to identify:

- Redundant API calls (same endpoint called multiple times).
- Large payloads that should be paginated.
- Slow endpoints that block UI (should be awaited with loading states).

## Automated Performance Metrics

### Integration Test Benchmarks

```dart
import 'package:flutter_test/flutter_test.dart';
import 'package:integration_test/integration_test.dart';

import 'package:my_app/main.dart' as app;

void main() {
  final binding = IntegrationTestWidgetsFlutterBinding.ensureInitialized();

  testWidgets('scrolling performance', (tester) async {
    app.main();
    await tester.pumpAndSettle();

    // Find the scrollable list
    final listFinder = find.byType(ListView);

    // Record a performance timeline
    await binding.traceAction(
      () async {
        // Fling scroll down
        await tester.fling(listFinder, const Offset(0, -500), 10000);
        await tester.pumpAndSettle();

        // Fling scroll up
        await tester.fling(listFinder, const Offset(0, 500), 10000);
        await tester.pumpAndSettle();
      },
      reportKey: 'scrolling_timeline',
    );
  });
}
```

Run the benchmark and generate a summary:

```bash
flutter test integration_test/performance_test.dart \
  --profile \
  --no-pub \
  -d <device-id>
```

### CI Performance Gates

```dart
// test/performance_gate_test.dart
import 'dart:convert';
import 'dart:io';

import 'package:flutter_test/flutter_test.dart';

void main() {
  test('frame build times within budget', () {
    final file = File('build/scrolling_timeline.timeline_summary.json');
    final summary =
        jsonDecode(file.readAsStringSync()) as Map<String, dynamic>;

    final avgBuildTime =
        (summary['average_frame_build_time_millis'] as num).toDouble();
    final p99BuildTime =
        (summary['99th_percentile_frame_build_time_millis'] as num).toDouble();
    final missedFrames = summary['frame_build_time_millis_count'] as int;

    expect(avgBuildTime, lessThan(8.0), reason: 'Average build time too high');
    expect(p99BuildTime, lessThan(16.0), reason: 'P99 build time too high');
    expect(missedFrames, lessThan(5), reason: 'Too many missed frames');
  });
}
```
