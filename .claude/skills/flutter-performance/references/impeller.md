# Impeller Rendering Engine

## What Impeller Is and Why It Matters

Impeller is Flutter's next-generation rendering engine, designed to eliminate
shader compilation jank that plagued the Skia-based renderer. Instead of
compiling shaders at runtime (causing unpredictable frame drops the first time
a visual effect is encountered), Impeller **pre-compiles all shaders at build
time** into a known set of pipelines. The result is predictable, consistent
frame timing from the very first frame.

Key design goals:

- **Predictable performance.** No first-run shader compilation stalls.
- **Modern GPU API usage.** Metal on iOS/macOS, Vulkan on Android (with
  OpenGL ES fallback).
- **Simpler architecture.** A purpose-built renderer for Flutter's scene graph
  rather than a general-purpose 2D library.

## Enabling Impeller

### iOS (Default since Flutter 3.16)

Impeller is the **default** renderer on iOS. No action needed.

To explicitly disable it (for debugging only):

```xml
<!-- ios/Runner/Info.plist -->
<key>FLTEnableImpeller</key>
<false/>
```

### Android (Opt-in, preview)

Impeller on Android targets Vulkan-capable devices. Enable it in
`AndroidManifest.xml`:

```xml
<!-- android/app/src/main/AndroidManifest.xml -->
<application>
  <meta-data
    android:name="io.flutter.embedding.android.EnableImpeller"
    android:value="true" />
</application>
```

Or pass a flag at run time:

```bash
flutter run --enable-impeller
```

> **Note:** Devices without Vulkan support fall back to the OpenGL ES backend
> automatically. Test on a range of devices.

### macOS and Linux (Experimental)

Impeller support on desktop platforms is under active development. Check the
Flutter release notes for the latest status.

## Impeller vs Skia Comparison

| Aspect | Skia | Impeller |
|---|---|---|
| Shader compilation | Runtime; causes first-run jank | Build time; zero runtime compilation |
| GPU backend (iOS) | OpenGL ES (deprecated by Apple) | Metal |
| GPU backend (Android) | OpenGL ES | Vulkan (OpenGL ES fallback) |
| Texture handling | CPU-side rasterization for some ops | GPU-accelerated throughout |
| Text rendering | Skia paragraph library | Impeller typography engine |
| Custom shaders (`FragmentProgram`) | Supported | Supported (GLSL compiled to target backend) |
| Binary size impact | Baseline | Slightly smaller on iOS (no Skia blob) |

## Shader Compilation Benefits

With Skia, the first time Flutter encounters a new combination of paint
operations it must compile an `SkSL` shader on the GPU driver -- a process
that can take 20-200 ms and causes visible jank. Workarounds like
`--cache-sksl` helped but required running the app through all visual paths
first.

Impeller eliminates this entirely:

```
# Before (Skia): warm-up needed
flutter run --profile --cache-sksl --purge-persistent-cache

# After (Impeller): no warm-up, no jank
flutter run --profile
```

In practice this means:

- Hero animations on first navigation are smooth.
- Complex `CustomPainter` widgets render without stutter.
- Automated performance tests produce consistent numbers across runs.

## Debugging Impeller Rendering

### Performance Overlay

The standard performance overlay works with Impeller:

```dart
MaterialApp(
  showPerformanceOverlay: true,
  // ...
);
```

The raster thread bar (green) should stay below the 16 ms line for 60 fps
targets.

### DevTools Layers View

Open the **Flutter Inspector** in DevTools and enable the **Layers** view to
see how Impeller composites the scene. Look for:

- Excessive layer count (each layer has GPU cost).
- Unexpected save-layer operations from `Opacity`, `ClipRRect`, or
  `BackdropFilter` widgets.

### Impeller-Specific Debug Flags

```bash
# Visualize overdraw regions
flutter run --profile --dart-define=FLUTTER_IMPELLER_OVERDRAW=true

# Dump Impeller pipeline statistics
flutter run --profile --verbose 2>&1 | grep -i impeller
```

### Checking Which Renderer Is Active

At runtime, confirm the active renderer:

```dart
import 'dart:ui' as ui;

void checkRenderer() {
  // In debug/profile mode, check the timeline events or DevTools.
  // There is no stable public API to query this at runtime.
  // Use DevTools > Flutter Inspector > Rendering backend.
  debugPrint('Impeller is enabled on iOS by default since Flutter 3.16');
}
```

## Custom Shader Support with Impeller

Flutter's `FragmentProgram` API works with Impeller. Write shaders in GLSL
and reference them in `pubspec.yaml`:

```yaml
# pubspec.yaml
flutter:
  shaders:
    - shaders/gradient_wave.frag
```

Example fragment shader:

```glsl
// shaders/gradient_wave.frag
#version 460 core

#include <flutter/runtime_effect.glsl>

uniform float uTime;
uniform vec2 uResolution;

out vec4 fragColor;

void main() {
  vec2 uv = FlutterFragCoord().xy / uResolution;
  float wave = sin(uv.x * 10.0 + uTime) * 0.5 + 0.5;
  fragColor = vec4(uv.x, wave, uv.y, 1.0);
}
```

Loading and using the shader in Dart:

```dart
import 'dart:ui' as ui;
import 'package:flutter/material.dart';

class ShaderPainter extends CustomPainter {
  ShaderPainter({
    required this.shader,
    required this.time,
    required this.size,
  });

  final ui.FragmentShader shader;
  final double time;
  final Size size;

  @override
  void paint(Canvas canvas, Size canvasSize) {
    shader
      ..setFloat(0, time)        // uTime
      ..setFloat(1, size.width)  // uResolution.x
      ..setFloat(2, size.height); // uResolution.y

    final paint = Paint()..shader = shader;
    canvas.drawRect(Offset.zero & canvasSize, paint);
  }

  @override
  bool shouldRepaint(ShaderPainter oldDelegate) =>
      oldDelegate.time != time;
}

class ShaderWidget extends StatefulWidget {
  const ShaderWidget({super.key});

  @override
  State<ShaderWidget> createState() => _ShaderWidgetState();
}

class _ShaderWidgetState extends State<ShaderWidget>
    with SingleTickerProviderStateMixin {
  late final AnimationController _controller;
  ui.FragmentProgram? _program;

  @override
  void initState() {
    super.initState();
    _controller = AnimationController(
      vsync: this,
      duration: const Duration(seconds: 10),
    )..repeat();
    _loadShader();
  }

  Future<void> _loadShader() async {
    final program = await ui.FragmentProgram.fromAsset(
      'shaders/gradient_wave.frag',
    );
    if (mounted) {
      setState(() => _program = program);
    }
  }

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final program = _program;
    if (program == null) {
      return const SizedBox.shrink();
    }

    return AnimatedBuilder(
      animation: _controller,
      builder: (context, child) {
        return CustomPaint(
          painter: ShaderPainter(
            shader: program.fragmentShader(),
            time: _controller.value * 10.0,
            size: MediaQuery.sizeOf(context),
          ),
        );
      },
    );
  }
}
```

> **Impeller note:** Impeller compiles GLSL to Metal (iOS) or Vulkan SPIR-V
> (Android) at build time. There is no runtime compilation penalty.

## Known Limitations and Workarounds

| Limitation | Details | Workaround |
|---|---|---|
| Android Vulkan requirement | Impeller's primary Android backend requires Vulkan 1.1+. Older devices use OpenGL ES fallback. | Test on both Vulkan and non-Vulkan devices. The fallback is automatic. |
| `BackdropFilter` cost | Backdrop filters remain expensive because they require reading back from the framebuffer. | Minimize `BackdropFilter` usage; prefer `ImageFiltered` on static content. Use `RepaintBoundary` to limit the read-back region. |
| `saveLayer` implicit costs | Widgets like `Opacity`, `ShaderMask`, and `ColorFiltered` trigger implicit save-layers. | Replace `Opacity` with `FadeTransition` or animate the color's alpha channel directly. |
| Platform view compositing | Platform views (maps, webviews) force texture composition that bypasses Impeller's pipeline. | Minimize platform view surface area; use `AndroidView` with `initAndroidView` for better layering. |
| Text rendering edge cases | Complex text shaping (RTL mixed with LTR, certain emoji sequences) may render differently. | File issues on the Flutter repo with reproduction cases. |
| Custom `dart:ui` Canvas ops | Some rarely used Canvas operations may not yet be optimized. | Profile specific paint operations and file issues if performance regresses vs Skia. |

## Performance Characteristics

Typical improvements observed when switching from Skia to Impeller:

- **99th percentile frame time** drops 30-60% on iOS due to elimination of
  shader compilation spikes.
- **Worst-frame time** improves dramatically (from 100+ ms to < 16 ms) on
  first-run scenarios.
- **Average frame time** is comparable or slightly better; the main win is
  consistency.
- **Memory usage** is generally similar; Impeller's texture caching strategy
  differs but does not significantly increase peak memory.
- **Binary size** may decrease slightly on iOS because the Skia binary blob is
  replaced by the smaller Impeller library.

Always validate these numbers on **your specific app** with `flutter run --profile`
and DevTools. General benchmarks are not a substitute for profiling your own
widget tree and paint operations.
