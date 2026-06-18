# Flutter DevTools

## Overview

Flutter DevTools is a suite of debugging and performance tools for Flutter and Dart applications. It runs in a browser and connects to a running Flutter app.

### Launching DevTools

```bash
# Option 1: From a running app in the terminal
flutter run
# Then press 'v' to open DevTools in the browser, or use the URL printed in the console

# Option 2: Activate and run globally
flutter pub global activate devtools
dart devtools

# Option 3: From VS Code
# DevTools is integrated -- use the command palette:
# "Dart: Open DevTools" or click the DevTools icon in the debug toolbar

# Option 4: From Android Studio / IntelliJ
# Use the Flutter Inspector tab, or open DevTools from the Run/Debug toolbar
```

---

## Widget Inspector

The Widget Inspector helps you visualize and explore the widget tree of your running app.

### Widget Tree

- Displays the full hierarchy of widgets in the current screen
- Click any widget in the tree to see its properties in the details panel
- Use "Select Widget Mode" to tap a widget in the app and jump to it in the tree
- Search the tree by widget type name

### Enabling Select Widget Mode

1. Open DevTools Widget Inspector
2. Click the "Select Widget Mode" button (crosshair icon) in the toolbar
3. Tap any widget in your running app
4. The widget tree highlights the selected widget and shows its properties

### Layout Explorer

The Layout Explorer is embedded within the Widget Inspector and visualizes how Flex widgets (Row, Column, Flex) lay out their children.

Key features:
- Visual representation of `mainAxisAlignment` and `crossAxisAlignment`
- Shows `flex` factors for each child
- Displays constraints (min/max width and height)
- Interactive -- you can modify properties and see changes live

How to use:
1. Select a `Row`, `Column`, or `Flex` widget in the widget tree
2. The Layout Explorer panel appears in the details pane
3. Hover over children to see their individual constraints
4. Click alignment dropdown to change alignment interactively

### Widget Details

When a widget is selected, the details panel shows:
- All constructor parameters and their current values
- The `RenderObject` properties (size, constraints, offsets)
- The widget's location in source code (click to open in IDE)

---

## Performance View

The Performance view helps identify rendering performance issues and jank.

### Frame Chart

- Displays a bar chart of each rendered frame
- Each bar has two segments: **Build** (blue) and **Raster** (green)
- A red dashed line marks the 16ms budget (60fps) or 8ms budget (120fps)
- Frames exceeding the budget line appear in red/orange

### Reading the Frame Chart

| Segment | Color | Meaning |
|---|---|---|
| Build | Blue | Time spent building the widget tree (Dart code on the UI thread) |
| Raster | Green | Time spent rasterizing the frame (GPU thread) |
| Over budget | Red/Orange | Frame took longer than the target frame time |

### Timeline Events

Click a frame bar to see its timeline events:
- **Build** phase: shows which widgets were rebuilt
- **Layout** phase: shows layout calculations
- **Paint** phase: shows paint operations
- **Compositing** phase: shows layer composition

### Jank Detection

Jank occurs when frames take longer than the frame budget. Common causes:

**Build jank (UI thread):**
- Rebuilding too many widgets (use `const` constructors, granular `setState`)
- Expensive computations in `build()` (move to isolates)
- Large lists without `ListView.builder`

**Raster jank (GPU thread):**
- Complex clipping paths (`ClipPath`)
- Excessive use of `Opacity` widget (use `AnimatedOpacity` or `FadeTransition`)
- Large images not properly cached or sized
- Expensive `saveLayer` operations (from `Opacity`, `ShaderMask`, `ColorFilter`)

### Enabling Performance Overlay

```dart
MaterialApp(
  showPerformanceOverlay: true, // Shows frame timing overlay on the app
  // ...
)
```

Or toggle from DevTools Performance view toolbar.

### Track Widget Builds

Enable "Track Widget Builds" in the Performance view to see exactly which widgets rebuild each frame. This helps identify unnecessary rebuilds.

### Best Practices for Performance Profiling

1. Always profile in **profile mode** (`flutter run --profile`), not debug mode
2. Use a physical device, not an emulator/simulator, for accurate GPU timelines
3. Avoid profiling with DevTools overlay open (it adds overhead)
4. Look for patterns of repeated jank, not isolated slow frames

---

## CPU Profiler

The CPU Profiler shows where your Dart code spends time.

### Recording a Profile

1. Open the CPU Profiler tab in DevTools
2. Click "Record" to start sampling
3. Interact with your app to trigger the code you want to profile
4. Click "Stop" to end the recording

### Reading Results

**Bottom-up view:**
- Shows methods sorted by self time (time spent in the method itself)
- Useful for finding which specific methods are slow

**Call tree (top-down) view:**
- Shows the call hierarchy from root to leaf
- Useful for understanding the call chain leading to slow methods

**Flame chart:**
- Visual representation of the call stack over time
- Width represents time spent in a method
- Deeper stacks show nested method calls

### Filtering

- Filter by package name to focus on your code (exclude framework internals)
- Filter by library to narrow down to specific files
- Use the search box to find specific method names

### Common CPU Performance Issues

| Pattern | Cause | Solution |
|---|---|---|
| Wide bar in `build()` | Expensive widget build | Break into smaller widgets, use `const` |
| Deep recursion | Recursive algorithms | Refactor to iterative, or use `compute()` |
| Repeated JSON parsing | Parsing on UI thread | Move to isolate with `compute()` |
| Sorting large lists | Sort in `build()` | Pre-sort and cache results |

---

## Memory View

The Memory view helps identify memory leaks, excessive allocations, and overall memory usage.

### Heap Overview

- **Used**: Memory currently allocated and in use
- **External**: Memory allocated outside the Dart heap (e.g., images, platform channels)
- **RSS (Resident Set Size)**: Total physical memory used by the process
- **GC events**: Marked on the timeline when garbage collection occurs

### Memory Chart

The memory chart shows usage over time:
- **Dart heap**: memory for Dart objects
- **External**: platform memory, images, etc.
- Spikes may indicate large allocations or leaks

### Taking a Heap Snapshot

1. Click "Snapshot" in the Memory tab
2. Wait for the snapshot to load
3. Browse objects by class name
4. Sort by "Retained Size" to find the largest memory consumers
5. Drill into object references to understand retention chains

### Allocation Tracking

1. Click "Start" under the Allocation Tracking section
2. Perform the actions you want to track
3. Click "Stop"
4. View which classes had the most allocations
5. Sort by count or size

### Detecting Memory Leaks

Common leak patterns and how to find them:

**StreamSubscription not cancelled:**
```dart
// LEAK: subscription never cancelled
class MyWidget extends StatefulWidget { ... }

class _MyWidgetState extends State<MyWidget> {
  late StreamSubscription _subscription;

  @override
  void initState() {
    super.initState();
    _subscription = myStream.listen((data) { /* ... */ });
  }

  // FIX: cancel in dispose
  @override
  void dispose() {
    _subscription.cancel();
    super.dispose();
  }
}
```

**AnimationController not disposed:**
```dart
class _MyWidgetState extends State<MyWidget> with SingleTickerProviderStateMixin {
  late AnimationController _controller;

  @override
  void initState() {
    super.initState();
    _controller = AnimationController(vsync: this, duration: const Duration(seconds: 1));
  }

  @override
  void dispose() {
    _controller.dispose(); // Must dispose to prevent leak
    super.dispose();
  }
}
```

**Using the Diff feature:**
1. Take snapshot A (baseline)
2. Navigate to a screen and back
3. Take snapshot B
4. Select "Diff snapshots" to see objects that were created and not freed

### Leak Detection

DevTools includes automatic leak detection (Flutter 3.18+):

- Leaks are displayed in the Memory view under the "Leaks" tab
- Two types: **not-disposed** (resource not cleaned up) and **not-GCed** (object retained unexpectedly)
- Click a leak to see the retaining path -- the chain of references keeping the object alive

---

## Network Profiler

The Network profiler records HTTP requests and responses from your app.

### What It Captures

- HTTP method (GET, POST, PUT, DELETE, etc.)
- URL
- Status code
- Request/response headers
- Request/response body
- Timing (start time, duration)
- Size (request and response body sizes)

### Usage

1. Open the Network tab in DevTools
2. Network requests are automatically recorded while the tab is open
3. Click a request to see full details
4. Use the search bar to filter by URL
5. Filter by status code (e.g., 4xx, 5xx)

### Requirements

- Works with `dart:io` `HttpClient`, `package:http`, and `package:dio`
- The app must be running in debug or profile mode
- Web apps use browser DevTools for network profiling instead

### Tips

- Look for repeated identical requests (missing caching)
- Check response sizes (compress large payloads)
- Monitor request durations to identify slow endpoints
- Check for failed requests (4xx/5xx status codes)

---

## Logging View

The Logging view displays structured log output from the app.

### What Appears in the Log

- `dart:developer` `log()` calls
- Framework events (`flutter.frame`, `flutter.navigation`, etc.)
- `print()` output
- Garbage collection events
- Custom service extension events

### Structured Logging

```dart
import 'dart:developer';

// Basic log
log('User logged in');

// Log with level and name
log(
  'Failed to fetch data',
  name: 'NetworkService',
  level: 1000, // Severe
  error: exception,
  stackTrace: stackTrace,
);

// Log levels:
// 0    = FINEST
// 300  = FINER
// 400  = FINE
// 500  = CONFIG
// 700  = INFO
// 800  = WARNING
// 900  = SEVERE
// 1000 = SHOUT
```

### Filtering Logs

- Filter by log level (drop-down in the toolbar)
- Filter by logger name (e.g., show only `NetworkService` logs)
- Search through log messages
- Toggle framework events on/off

### Navigation Events

DevTools logs navigation events automatically:

```
flutter.navigation: {route: /home}
flutter.navigation: {route: /profile/123}
```

This helps track route transitions without adding manual logging.

---

## App Size Tool

The App Size tool helps you understand and reduce your app's compiled size.

### Generating Size Analysis Data

```bash
# Generate size analysis for Android
flutter build appbundle --analyze-size

# Generate size analysis for iOS
flutter build ios --analyze-size

# Generate size analysis for web
flutter build web --analyze-size

# The command outputs a JSON file path for analysis
# Example: build/android-code-size-analysis_01.json
```

### Using the App Size Tool

1. Open DevTools and go to the App Size tab
2. Click "Upload" and select the generated JSON file
3. Browse the tree map to see which packages and libraries consume the most space

### Tree Map View

- **Large boxes** = large contributors to app size
- **Colors** represent different categories (Dart code, native libraries, assets, etc.)
- Click to drill down into packages and files

### Diff Mode

Compare sizes between two builds:

1. Generate analysis files for both builds (e.g., before and after a change)
2. Upload both files to the App Size tool "Diff" tab
3. See exactly what increased or decreased in size

### Common Size Optimization Strategies

| Strategy | Command/Approach |
|---|---|
| Enable tree shaking | `flutter build --tree-shake-icons` |
| Remove unused packages | `flutter pub deps` then remove unneeded deps |
| Use deferred/lazy loading | `import 'package:x/y.dart' deferred as y;` |
| Compress images | Use WebP format, appropriate resolution |
| Split APKs by ABI | `flutter build apk --split-per-abi` |
| Enable code obfuscation | `--obfuscate --split-debug-info=DIR` |
| Analyze Dart code size | Review the "Dart AOT" section in the tree map |
| Reduce font packages | Only include needed font weights/styles |

---

## Using DevTools with VS Code

### Setup

1. Install the **Dart** and **Flutter** extensions from the VS Code marketplace
2. DevTools is bundled and launches automatically when debugging

### Opening DevTools

- **Command Palette**: `Dart: Open DevTools`
- **Debug Toolbar**: Click the DevTools icon during a debug session
- **Sidebar**: The Flutter panel shows a "Open DevTools" button

### Embedded DevTools

VS Code embeds some DevTools panels directly:

- **Widget Inspector**: Available in the sidebar during debugging
- **DevTools Browser**: Opens in a VS Code tab (not an external browser)
- **Dart DevTools**: Click specific tool icons in the debug toolbar

### VS Code Settings for DevTools

```json
// .vscode/settings.json
{
  "dart.devToolsTheme": "dark",
  "dart.openDevTools": "flutter",
  "dart.embedDevTools": true,
  "dart.devToolsBrowser": "chrome",
  "dart.devToolsLocation": "beside"
}
```

| Setting | Values | Description |
|---|---|---|
| `dart.openDevTools` | `"never"`, `"flutter"`, `"always"` | When to auto-open DevTools |
| `dart.embedDevTools` | `true`, `false` | Embed in VS Code vs. external browser |
| `dart.devToolsLocation` | `"beside"`, `"active"`, `"external"` | Where to open the DevTools panel |

---

## Using DevTools with Android Studio / IntelliJ

### Setup

1. Install the **Flutter** plugin (includes Dart plugin)
2. DevTools integrates into the IDE automatically

### Opening DevTools

- **Flutter Inspector** tab at the bottom of the IDE (visible during a Flutter run/debug session)
- **Run menu** > "Open Flutter DevTools" or use the toolbar button
- **Actions** menu: Search "Flutter DevTools"

### Embedded Panels

Android Studio embeds several DevTools panels:

- **Flutter Inspector**: Widget tree and layout explorer in the IDE sidebar
- **Flutter Performance**: Frame chart and timeline in the IDE
- **Flutter Outline**: Structure view of the widget tree from source code

### IDE-Specific Features

**Flutter Inspector in the Sidebar:**
- Live widget tree with property details
- Layout Explorer for Flex widgets
- "Select Widget Mode" with click-to-inspect
- Render object details (size, position, constraints)

**Flutter Performance Window:**
- Frame chart with build and raster times
- Track Widget Builds toggle
- Performance overlay toggle

### Tips for Android Studio Users

- Use the **Flutter Outline** view alongside the editor to navigate the widget tree from code
- The **Structure** view shows the Dart file structure (classes, methods, fields)
- Right-click a widget in the editor and select "Open in Flutter Inspector" to jump to it in the live widget tree
- Use **Layout Inspector** (under View > Tool Windows) for native Android view inspection alongside Flutter DevTools
