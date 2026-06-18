# Fragment Shaders Reference

## Table of Contents

1. [Flutter Fragment Shader Support](#flutter-fragment-shader-support)
2. [Loading .frag Files](#loading-frag-files)
3. [FragmentProgram and FragmentShader](#fragmentprogram-and-fragmentshader)
4. [Passing Uniforms](#passing-uniforms)
5. [Animated Shader Effects](#animated-shader-effects)
6. [Common Shader Patterns](#common-shader-patterns)
7. [Performance Considerations](#performance-considerations)

---

## Flutter Fragment Shader Support

Flutter supports GLSL ES 1.0 fragment shaders compiled at build time via the `shaders` key in `pubspec.yaml`. These shaders run on the GPU and are ideal for effects like gradients, noise, blur, water ripples, and color manipulation.

### pubspec.yaml configuration

```yaml
flutter:
  shaders:
    - shaders/ripple.frag
    - shaders/noise.frag
    - shaders/gradient_sweep.frag
```

Place `.frag` files in a `shaders/` directory at the project root (or any path you declare in `pubspec.yaml`).

### Shader file skeleton

Every Flutter fragment shader must:

1. Declare `precision mediump float;` (or `highp`).
2. Write to `fragColor` (the output variable).
3. Receive Flutter-injected `gl_FragCoord` for pixel coordinates.

```glsl
// shaders/solid_color.frag
#version 460 core

#include <flutter/runtime_effect.glsl>

uniform vec2 uResolution;
uniform vec4 uColor;

out vec4 fragColor;

void main() {
  fragColor = uColor;
}
```

> **Note:** Flutter uses `#include <flutter/runtime_effect.glsl>` to inject the necessary runtime definitions. This is required in every shader file.

---

## Loading .frag Files

Use `FragmentProgram.fromAsset` to load a compiled shader at runtime.

```dart
import 'dart:ui' as ui;
import 'package:flutter/material.dart';

class ShaderLoader {
  ShaderLoader._();

  static Future<ui.FragmentProgram> load(String assetKey) async {
    return ui.FragmentProgram.fromAsset(assetKey);
  }
}
```

### Loading in a widget

```dart
import 'dart:ui' as ui;
import 'package:flutter/material.dart';

class ShaderWidget extends StatefulWidget {
  const ShaderWidget({super.key});

  @override
  State<ShaderWidget> createState() => _ShaderWidgetState();
}

class _ShaderWidgetState extends State<ShaderWidget> {
  ui.FragmentProgram? _program;

  @override
  void initState() {
    super.initState();
    _loadShader();
  }

  Future<void> _loadShader() async {
    final program = await ui.FragmentProgram.fromAsset('shaders/ripple.frag');
    if (mounted) {
      setState(() => _program = program);
    }
  }

  @override
  Widget build(BuildContext context) {
    final program = _program;
    if (program == null) {
      return const SizedBox.shrink();
    }
    return CustomPaint(
      painter: RippleShaderPainter(program: program, time: 0),
      size: const Size(400, 400),
    );
  }
}
```

---

## FragmentProgram and FragmentShader

`FragmentProgram` is the compiled shader program. Call `fragmentShader()` on it to obtain a `FragmentShader` instance, which is a `Shader` that can be assigned to `Paint.shader`.

```dart
class SimpleShaderPainter extends CustomPainter {
  SimpleShaderPainter({required this.program});

  final ui.FragmentProgram program;

  @override
  void paint(Canvas canvas, Size size) {
    final shader = program.fragmentShader();

    // Set uniforms (order must match the shader source).
    shader.setFloat(0, size.width);  // uResolution.x
    shader.setFloat(1, size.height); // uResolution.y

    canvas.drawRect(
      Offset.zero & size,
      Paint()..shader = shader,
    );

    shader.dispose();
  }

  @override
  bool shouldRepaint(covariant SimpleShaderPainter oldDelegate) {
    return oldDelegate.program != program;
  }
}
```

### Uniform Indexing

Uniforms are set by **float index**, not by name. Each `float` occupies one index. A `vec2` occupies two consecutive indices, `vec3` three, `vec4` four, and so on.

Given the GLSL:

```glsl
uniform vec2 uResolution;  // index 0, 1
uniform float uTime;        // index 2
uniform vec4 uColor;        // index 3, 4, 5, 6
```

The Dart side:

```dart
shader.setFloat(0, size.width);   // uResolution.x
shader.setFloat(1, size.height);  // uResolution.y
shader.setFloat(2, time);         // uTime
shader.setFloat(3, r);            // uColor.r
shader.setFloat(4, g);            // uColor.g
shader.setFloat(5, b);            // uColor.b
shader.setFloat(6, a);            // uColor.a
```

### Passing Images (Samplers)

You can pass `dart:ui` `Image` objects to shaders as samplers.

```dart
shader.setImageSampler(0, myUiImage);
```

In the GLSL:

```glsl
uniform sampler2D uTexture;
```

---

## Passing Uniforms

### Time, Resolution, and Colors

A full example passing the three most common uniform types.

**GLSL (`shaders/wave.frag`):**

```glsl
#version 460 core

#include <flutter/runtime_effect.glsl>

uniform vec2 uResolution;
uniform float uTime;
uniform vec4 uColor;

out vec4 fragColor;

void main() {
  vec2 uv = FlutterFragCoord().xy / uResolution;

  // Sine wave distortion.
  float wave = sin(uv.x * 10.0 + uTime * 3.0) * 0.05;
  float intensity = smoothstep(0.45 + wave, 0.55 + wave, uv.y);

  fragColor = mix(uColor, vec4(0.0), intensity);
}
```

**Dart painter:**

```dart
import 'dart:ui' as ui;
import 'package:flutter/material.dart';

class WaveShaderPainter extends CustomPainter {
  WaveShaderPainter({
    required this.program,
    required this.time,
    required this.color,
  });

  final ui.FragmentProgram program;
  final double time;
  final Color color;

  @override
  void paint(Canvas canvas, Size size) {
    final shader = program.fragmentShader();

    // vec2 uResolution
    shader.setFloat(0, size.width);
    shader.setFloat(1, size.height);

    // float uTime
    shader.setFloat(2, time);

    // vec4 uColor
    shader.setFloat(3, color.red / 255.0);
    shader.setFloat(4, color.green / 255.0);
    shader.setFloat(5, color.blue / 255.0);
    shader.setFloat(6, color.alpha / 255.0);

    canvas.drawRect(Offset.zero & size, Paint()..shader = shader);
    shader.dispose();
  }

  @override
  bool shouldRepaint(covariant WaveShaderPainter oldDelegate) {
    return oldDelegate.time != time ||
        oldDelegate.color != color ||
        oldDelegate.program != program;
  }
}
```

---

## Animated Shader Effects

Combine an `AnimationController` with a shader painter for smooth GPU-driven animations.

```dart
import 'dart:ui' as ui;
import 'package:flutter/material.dart';

class AnimatedShaderWidget extends StatefulWidget {
  const AnimatedShaderWidget({super.key});

  @override
  State<AnimatedShaderWidget> createState() => _AnimatedShaderWidgetState();
}

class _AnimatedShaderWidgetState extends State<AnimatedShaderWidget>
    with SingleTickerProviderStateMixin {
  late final AnimationController _controller;
  ui.FragmentProgram? _program;
  double _elapsed = 0;

  @override
  void initState() {
    super.initState();
    _controller = AnimationController(
      vsync: this,
      // A long upper bound so `value` acts as a continuous ticker.
      duration: const Duration(seconds: 3600),
    );
    _loadShader();
  }

  Future<void> _loadShader() async {
    final program = await ui.FragmentProgram.fromAsset('shaders/wave.frag');
    if (mounted) {
      setState(() => _program = program);
      _controller
        ..addListener(_onTick)
        ..forward();
    }
  }

  void _onTick() {
    // Convert controller value (0..1 over 3600s) to elapsed seconds.
    _elapsed = _controller.value * 3600;
    // Force repaint via the repaint listenable.
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
      return const SizedBox(width: 400, height: 400);
    }

    return RepaintBoundary(
      child: AnimatedBuilder(
        animation: _controller,
        builder: (context, _) {
          return CustomPaint(
            painter: WaveShaderPainter(
              program: program,
              time: _elapsed,
              color: Theme.of(context).colorScheme.primary,
            ),
            size: const Size(400, 400),
          );
        },
      ),
    );
  }
}
```

> **Tip:** For the ticker pattern above, an alternative approach is to use a `Ticker` directly from `TickerProviderStateMixin` and track elapsed time via `Duration`. This avoids the arbitrary 3600-second upper bound.

### Ticker-Based Alternative

```dart
import 'dart:ui' as ui;
import 'package:flutter/material.dart';
import 'package:flutter/scheduler.dart';

class TickerShaderWidget extends StatefulWidget {
  const TickerShaderWidget({super.key});

  @override
  State<TickerShaderWidget> createState() => _TickerShaderWidgetState();
}

class _TickerShaderWidgetState extends State<TickerShaderWidget>
    with SingleTickerProviderStateMixin {
  late final Ticker _ticker;
  ui.FragmentProgram? _program;
  double _elapsedSeconds = 0;

  @override
  void initState() {
    super.initState();
    _ticker = createTicker(_onTick);
    _loadShader();
  }

  Future<void> _loadShader() async {
    final program = await ui.FragmentProgram.fromAsset('shaders/wave.frag');
    if (mounted) {
      setState(() => _program = program);
      _ticker.start();
    }
  }

  void _onTick(Duration elapsed) {
    setState(() {
      _elapsedSeconds = elapsed.inMicroseconds / 1e6;
    });
  }

  @override
  void dispose() {
    _ticker.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final program = _program;
    if (program == null) {
      return const SizedBox(width: 400, height: 400);
    }

    return RepaintBoundary(
      child: CustomPaint(
        painter: WaveShaderPainter(
          program: program,
          time: _elapsedSeconds,
          color: Theme.of(context).colorScheme.primary,
        ),
        size: const Size(400, 400),
      ),
    );
  }
}
```

---

## Common Shader Patterns

### Radial Gradient (pure shader)

```glsl
#version 460 core

#include <flutter/runtime_effect.glsl>

uniform vec2 uResolution;
uniform vec4 uColorInner;
uniform vec4 uColorOuter;

out vec4 fragColor;

void main() {
  vec2 uv = FlutterFragCoord().xy / uResolution;
  vec2 center = vec2(0.5);
  float dist = distance(uv, center);

  fragColor = mix(uColorInner, uColorOuter, smoothstep(0.0, 0.5, dist));
}
```

### Simplex Noise

```glsl
#version 460 core

#include <flutter/runtime_effect.glsl>

uniform vec2 uResolution;
uniform float uTime;

out vec4 fragColor;

// Attempt at a simple pseudo-noise using sine-based hash.
vec2 hash(vec2 p) {
  p = vec2(dot(p, vec2(127.1, 311.7)),
           dot(p, vec2(269.5, 183.3)));
  return -1.0 + 2.0 * fract(sin(p) * 43758.5453123);
}

float noise(vec2 p) {
  vec2 i = floor(p);
  vec2 f = fract(p);
  vec2 u = f * f * (3.0 - 2.0 * f);

  return mix(
    mix(dot(hash(i + vec2(0.0, 0.0)), f - vec2(0.0, 0.0)),
        dot(hash(i + vec2(1.0, 0.0)), f - vec2(1.0, 0.0)), u.x),
    mix(dot(hash(i + vec2(0.0, 1.0)), f - vec2(0.0, 1.0)),
        dot(hash(i + vec2(1.0, 1.0)), f - vec2(1.0, 1.0)), u.x),
    u.y
  );
}

void main() {
  vec2 uv = FlutterFragCoord().xy / uResolution;
  float n = noise(uv * 8.0 + uTime);
  float brightness = 0.5 + 0.5 * n;

  fragColor = vec4(vec3(brightness), 1.0);
}
```

### Directional Blur (box blur approximation)

```glsl
#version 460 core

#include <flutter/runtime_effect.glsl>

uniform vec2 uResolution;
uniform float uBlurRadius;
uniform sampler2D uTexture;

out vec4 fragColor;

void main() {
  vec2 uv = FlutterFragCoord().xy / uResolution;
  vec2 texelSize = 1.0 / uResolution;

  vec4 color = vec4(0.0);
  float total = 0.0;

  int radius = int(uBlurRadius);
  for (int x = -radius; x <= radius; x++) {
    for (int y = -radius; y <= radius; y++) {
      vec2 offset = vec2(float(x), float(y)) * texelSize;
      color += texture(uTexture, uv + offset);
      total += 1.0;
    }
  }

  fragColor = color / total;
}
```

Dart side to pass the image sampler:

```dart
@override
void paint(Canvas canvas, Size size) {
  final shader = program.fragmentShader();

  shader.setFloat(0, size.width);   // uResolution.x
  shader.setFloat(1, size.height);  // uResolution.y
  shader.setFloat(2, 3.0);          // uBlurRadius
  shader.setImageSampler(0, sourceImage); // uTexture

  canvas.drawRect(Offset.zero & size, Paint()..shader = shader);
  shader.dispose();
}
```

### Sweep Gradient with Rotation

```glsl
#version 460 core

#include <flutter/runtime_effect.glsl>

uniform vec2 uResolution;
uniform float uTime;
uniform vec4 uColor1;
uniform vec4 uColor2;

out vec4 fragColor;

void main() {
  vec2 uv = (FlutterFragCoord().xy / uResolution) - 0.5;
  float angle = atan(uv.y, uv.x) + uTime;
  float t = (angle / 6.28318) + 0.5; // Normalise to [0, 1].

  fragColor = mix(uColor1, uColor2, fract(t));
}
```

---

## Performance Considerations

### Shader Compilation

- Shaders are compiled at build time and bundled as assets. First-time `fromAsset` loads may incur a brief decode cost; cache the `FragmentProgram` reference.
- Avoid calling `FragmentProgram.fromAsset` in `build()`. Load once in `initState` or via a `FutureBuilder`.

### Shader Disposal

Always call `shader.dispose()` after drawing to free the GPU handle.

```dart
@override
void paint(Canvas canvas, Size size) {
  final shader = program.fragmentShader();
  // ... set uniforms ...
  canvas.drawRect(Offset.zero & size, Paint()..shader = shader);
  shader.dispose(); // Free GPU resources.
}
```

### Avoid Excessive Uniform Updates

Each `setFloat` call crosses the Dart-to-native boundary. Minimise calls by only updating changed uniforms when possible. For most interactive shaders the cost is negligible, but if you are setting dozens of uniforms per frame in a tight loop, consider restructuring the shader to pack data into fewer uniforms.

### Use `RepaintBoundary`

Wrap shader-painted widgets in `RepaintBoundary` so other UI changes do not trigger shader repaints.

### Shader Limitations in Flutter

- Only fragment shaders are supported (no vertex shaders).
- GLSL ES 1.0 feature set with Flutter-specific extensions.
- Maximum texture size depends on the device GPU.
- No compute shaders.
- Debugging tools are limited; use ShaderToy or similar tools to prototype shaders before porting to Flutter.
