# Explicit Animations

Explicit animations give you full control over the animation lifecycle through `AnimationController`. Use them when you need to play, pause, reverse, repeat, chain, or drive animations from gestures.

---

## AnimationController Setup with TickerProviderStateMixin

Every `AnimationController` requires a `TickerProvider` (the `vsync` parameter) to drive its frame callbacks. Use `SingleTickerProviderStateMixin` for a single controller or `TickerProviderStateMixin` for multiple controllers.

```dart
import 'package:flutter/material.dart';

class PulseAnimation extends StatefulWidget {
  const PulseAnimation({super.key});

  @override
  State<PulseAnimation> createState() => _PulseAnimationState();
}

class _PulseAnimationState extends State<PulseAnimation>
    with SingleTickerProviderStateMixin {
  late final AnimationController _controller;

  @override
  void initState() {
    super.initState();
    _controller = AnimationController(
      vsync: this,
      duration: const Duration(milliseconds: 600),
    )..repeat(reverse: true);
  }

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return AnimatedBuilder(
      animation: _controller,
      builder: (context, child) {
        return Transform.scale(
          scale: 0.8 + (_controller.value * 0.4),
          child: child,
        );
      },
      child: Container(
        width: 100,
        height: 100,
        decoration: const BoxDecoration(
          color: Colors.red,
          shape: BoxShape.circle,
        ),
      ),
    );
  }
}
```

### Multiple Controllers with TickerProviderStateMixin

```dart
import 'package:flutter/material.dart';

class MultiControllerExample extends StatefulWidget {
  const MultiControllerExample({super.key});

  @override
  State<MultiControllerExample> createState() =>
      _MultiControllerExampleState();
}

class _MultiControllerExampleState extends State<MultiControllerExample>
    with TickerProviderStateMixin {
  late final AnimationController _scaleController;
  late final AnimationController _rotationController;

  @override
  void initState() {
    super.initState();
    _scaleController = AnimationController(
      vsync: this,
      duration: const Duration(milliseconds: 800),
    );
    _rotationController = AnimationController(
      vsync: this,
      duration: const Duration(seconds: 2),
    )..repeat();
  }

  @override
  void dispose() {
    _scaleController.dispose();
    _rotationController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return GestureDetector(
      onTap: () {
        if (_scaleController.isCompleted) {
          _scaleController.reverse();
        } else {
          _scaleController.forward();
        }
      },
      child: AnimatedBuilder(
        animation: Listenable.merge([_scaleController, _rotationController]),
        builder: (context, child) {
          return Transform.scale(
            scale: 0.5 + (_scaleController.value * 0.5),
            child: Transform.rotate(
              angle: _rotationController.value * 2 * 3.14159,
              child: child,
            ),
          );
        },
        child: const FlutterLogo(size: 100),
      ),
    );
  }
}
```

---

## Tween Types

Tweens define the range and type of interpolation. Flutter provides several built-in tween classes.

### Tween<double>

The most common tween. Interpolates a double between `begin` and `end`.

```dart
late final Animation<double> _opacity;

@override
void initState() {
  super.initState();
  _controller = AnimationController(
    vsync: this,
    duration: const Duration(milliseconds: 500),
  );
  _opacity = Tween<double>(begin: 0.0, end: 1.0).animate(_controller);
}
```

### ColorTween

Interpolates between two colors through the color space.

```dart
late final Animation<Color?> _colorAnimation;

@override
void initState() {
  super.initState();
  _controller = AnimationController(
    vsync: this,
    duration: const Duration(milliseconds: 600),
  );
  _colorAnimation = ColorTween(
    begin: Colors.blue,
    end: Colors.orange,
  ).animate(_controller);
}
```

### IntTween

Interpolates between two integers (useful for counters, discrete steps).

```dart
late final Animation<int> _countAnimation;

@override
void initState() {
  super.initState();
  _controller = AnimationController(
    vsync: this,
    duration: const Duration(seconds: 1),
  );
  _countAnimation = IntTween(begin: 0, end: 100).animate(_controller);
}
```

### SizeTween

Interpolates between two `Size` values.

```dart
late final Animation<Size?> _sizeAnimation;

@override
void initState() {
  super.initState();
  _controller = AnimationController(
    vsync: this,
    duration: const Duration(milliseconds: 400),
  );
  _sizeAnimation = SizeTween(
    begin: const Size(80, 80),
    end: const Size(200, 200),
  ).animate(_controller);
}
```

### RectTween

Interpolates between two `Rect` values. Useful for position and size animations combined.

```dart
late final Animation<Rect?> _rectAnimation;

@override
void initState() {
  super.initState();
  _controller = AnimationController(
    vsync: this,
    duration: const Duration(milliseconds: 500),
  );
  _rectAnimation = RectTween(
    begin: const Rect.fromLTWH(0, 0, 50, 50),
    end: const Rect.fromLTWH(100, 100, 150, 150),
  ).animate(_controller);
}
```

---

## CurvedAnimation

Wraps an animation with a non-linear curve. You can specify different curves for the forward and reverse directions.

```dart
import 'package:flutter/material.dart';

class CurvedAnimationExample extends StatefulWidget {
  const CurvedAnimationExample({super.key});

  @override
  State<CurvedAnimationExample> createState() =>
      _CurvedAnimationExampleState();
}

class _CurvedAnimationExampleState extends State<CurvedAnimationExample>
    with SingleTickerProviderStateMixin {
  late final AnimationController _controller;
  late final Animation<double> _curvedAnimation;

  @override
  void initState() {
    super.initState();
    _controller = AnimationController(
      vsync: this,
      duration: const Duration(milliseconds: 600),
    );

    _curvedAnimation = CurvedAnimation(
      parent: _controller,
      curve: Curves.easeOutBack,
      reverseCurve: Curves.easeInBack,
    );
  }

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return GestureDetector(
      onTap: () {
        if (_controller.isCompleted) {
          _controller.reverse();
        } else {
          _controller.forward();
        }
      },
      child: AnimatedBuilder(
        animation: _curvedAnimation,
        builder: (context, child) {
          return Transform.scale(
            scale: _curvedAnimation.value,
            child: child,
          );
        },
        child: Container(
          width: 120,
          height: 120,
          decoration: BoxDecoration(
            color: Colors.teal,
            borderRadius: BorderRadius.circular(16),
          ),
          alignment: Alignment.center,
          child: const Text(
            'Tap',
            style: TextStyle(color: Colors.white, fontSize: 20),
          ),
        ),
      ),
    );
  }
}
```

### Combining Tween with CurvedAnimation

```dart
late final Animation<Offset> _slideAnimation;

@override
void initState() {
  super.initState();
  _controller = AnimationController(
    vsync: this,
    duration: const Duration(milliseconds: 500),
  );

  final curvedAnimation = CurvedAnimation(
    parent: _controller,
    curve: Curves.fastOutSlowIn,
  );

  _slideAnimation = Tween<Offset>(
    begin: const Offset(-1.0, 0.0),
    end: Offset.zero,
  ).animate(curvedAnimation);
}
```

---

## AnimatedBuilder

`AnimatedBuilder` is the recommended way to use explicit animations. It separates the animation logic from the widget tree, preventing unnecessary rebuilds of the child.

```dart
import 'package:flutter/material.dart';

class SlideInCard extends StatefulWidget {
  const SlideInCard({super.key});

  @override
  State<SlideInCard> createState() => _SlideInCardState();
}

class _SlideInCardState extends State<SlideInCard>
    with SingleTickerProviderStateMixin {
  late final AnimationController _controller;
  late final Animation<Offset> _offsetAnimation;
  late final Animation<double> _fadeAnimation;

  @override
  void initState() {
    super.initState();
    _controller = AnimationController(
      vsync: this,
      duration: const Duration(milliseconds: 500),
    );

    _offsetAnimation = Tween<Offset>(
      begin: const Offset(0, 0.3),
      end: Offset.zero,
    ).animate(
      CurvedAnimation(parent: _controller, curve: Curves.easeOutCubic),
    );

    _fadeAnimation = Tween<double>(begin: 0.0, end: 1.0).animate(
      CurvedAnimation(parent: _controller, curve: Curves.easeIn),
    );

    _controller.forward();
  }

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return AnimatedBuilder(
      animation: _controller,
      builder: (context, child) {
        return FractionalTranslation(
          translation: _offsetAnimation.value,
          child: Opacity(
            opacity: _fadeAnimation.value,
            child: child,
          ),
        );
      },
      child: Card(
        elevation: 4,
        child: Padding(
          padding: const EdgeInsets.all(24),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              const Icon(Icons.check_circle, color: Colors.green, size: 48),
              const SizedBox(height: 12),
              Text(
                'Animation Complete',
                style: Theme.of(context).textTheme.titleMedium,
              ),
            ],
          ),
        ),
      ),
    );
  }
}
```

---

## Chained Animations with addStatusListener

Use `addStatusListener` to trigger subsequent animations when one completes.

```dart
import 'package:flutter/material.dart';

class ChainedAnimationExample extends StatefulWidget {
  const ChainedAnimationExample({super.key});

  @override
  State<ChainedAnimationExample> createState() =>
      _ChainedAnimationExampleState();
}

class _ChainedAnimationExampleState extends State<ChainedAnimationExample>
    with TickerProviderStateMixin {
  late final AnimationController _slideController;
  late final AnimationController _fadeController;
  late final AnimationController _scaleController;

  late final Animation<Offset> _slideAnimation;
  late final Animation<double> _fadeAnimation;
  late final Animation<double> _scaleAnimation;

  @override
  void initState() {
    super.initState();

    // Step 1: Slide in
    _slideController = AnimationController(
      vsync: this,
      duration: const Duration(milliseconds: 400),
    );
    _slideAnimation = Tween<Offset>(
      begin: const Offset(-1, 0),
      end: Offset.zero,
    ).animate(
      CurvedAnimation(parent: _slideController, curve: Curves.easeOut),
    );

    // Step 2: Fade in (triggered after slide completes)
    _fadeController = AnimationController(
      vsync: this,
      duration: const Duration(milliseconds: 300),
    );
    _fadeAnimation = Tween<double>(begin: 0, end: 1).animate(_fadeController);

    // Step 3: Scale up (triggered after fade completes)
    _scaleController = AnimationController(
      vsync: this,
      duration: const Duration(milliseconds: 250),
    );
    _scaleAnimation = Tween<double>(begin: 0.8, end: 1.0).animate(
      CurvedAnimation(parent: _scaleController, curve: Curves.easeOutBack),
    );

    // Chain: slide -> fade -> scale
    _slideController.addStatusListener((status) {
      if (status == AnimationStatus.completed) {
        _fadeController.forward();
      }
    });

    _fadeController.addStatusListener((status) {
      if (status == AnimationStatus.completed) {
        _scaleController.forward();
      }
    });
  }

  void _playChain() {
    _slideController.reset();
    _fadeController.reset();
    _scaleController.reset();
    _slideController.forward();
  }

  @override
  void dispose() {
    _slideController.dispose();
    _fadeController.dispose();
    _scaleController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return Column(
      mainAxisAlignment: MainAxisAlignment.center,
      children: [
        AnimatedBuilder(
          animation:
              Listenable.merge([_slideController, _fadeController, _scaleController]),
          builder: (context, child) {
            return FractionalTranslation(
              translation: _slideAnimation.value,
              child: Opacity(
                opacity: _fadeAnimation.value,
                child: Transform.scale(
                  scale: _scaleAnimation.value,
                  child: child,
                ),
              ),
            );
          },
          child: Container(
            width: 200,
            height: 80,
            decoration: BoxDecoration(
              color: Colors.deepPurple,
              borderRadius: BorderRadius.circular(12),
            ),
            alignment: Alignment.center,
            child: const Text(
              'Chained!',
              style: TextStyle(
                color: Colors.white,
                fontSize: 20,
                fontWeight: FontWeight.bold,
              ),
            ),
          ),
        ),
        const SizedBox(height: 32),
        ElevatedButton(
          onPressed: _playChain,
          child: const Text('Play Chain'),
        ),
      ],
    );
  }
}
```

---

## Repeating and Reversing Animations

```dart
// Repeat forever
_controller.repeat();

// Repeat with reverse (ping-pong)
_controller.repeat(reverse: true);

// Repeat a specific number of times
void _repeatNTimes(int n) {
  var count = 0;
  _controller.addStatusListener((status) {
    if (status == AnimationStatus.completed) {
      count++;
      if (count < n) {
        _controller.reverse();
      }
    } else if (status == AnimationStatus.dismissed && count < n) {
      _controller.forward();
    }
  });
  _controller.forward();
}

// Play forward then reverse once
_controller.forward().then((_) => _controller.reverse());
```

### Full Repeating Animation Widget

```dart
import 'package:flutter/material.dart';

class SpinningLoader extends StatefulWidget {
  const SpinningLoader({super.key, this.size = 48});

  final double size;

  @override
  State<SpinningLoader> createState() => _SpinningLoaderState();
}

class _SpinningLoaderState extends State<SpinningLoader>
    with SingleTickerProviderStateMixin {
  late final AnimationController _controller;

  @override
  void initState() {
    super.initState();
    _controller = AnimationController(
      vsync: this,
      duration: const Duration(seconds: 1),
    )..repeat();
  }

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return AnimatedBuilder(
      animation: _controller,
      builder: (context, child) {
        return Transform.rotate(
          angle: _controller.value * 2 * 3.14159,
          child: child,
        );
      },
      child: Icon(
        Icons.refresh,
        size: widget.size,
        color: Theme.of(context).colorScheme.primary,
      ),
    );
  }
}
```

---

## Custom Animatable

Extend `Animatable<T>` for interpolation logic that goes beyond simple linear tweens.

```dart
import 'package:flutter/material.dart';

/// An Animatable that produces a zigzag pattern between two values.
class ZigzagTween extends Animatable<double> {
  const ZigzagTween({
    required this.min,
    required this.max,
    this.zigzagCount = 3,
  });

  final double min;
  final double max;
  final int zigzagCount;

  @override
  double transform(double t) {
    // Create a zigzag by mapping t through a triangle wave
    final period = 1.0 / zigzagCount;
    final phase = (t % period) / period;
    final triangleWave = phase < 0.5 ? phase * 2 : 2 - phase * 2;
    return min + (max - min) * triangleWave;
  }
}

// Usage:
// final zigzag = ZigzagTween(min: -10, max: 10, zigzagCount: 4);
// final animation = zigzag.animate(_controller);
```

### Using Custom Animatable in a Widget

```dart
import 'package:flutter/material.dart';

class ShakeWidget extends StatefulWidget {
  const ShakeWidget({super.key, required this.child});

  final Widget child;

  @override
  State<ShakeWidget> createState() => _ShakeWidgetState();
}

class _ShakeWidgetState extends State<ShakeWidget>
    with SingleTickerProviderStateMixin {
  late final AnimationController _controller;
  late final Animation<double> _shakeAnimation;

  @override
  void initState() {
    super.initState();
    _controller = AnimationController(
      vsync: this,
      duration: const Duration(milliseconds: 500),
    );

    _shakeAnimation = const ZigzagTween(
      min: -8,
      max: 8,
      zigzagCount: 4,
    ).animate(
      CurvedAnimation(parent: _controller, curve: Curves.easeOut),
    );
  }

  void shake() {
    _controller.forward(from: 0);
  }

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return AnimatedBuilder(
      animation: _shakeAnimation,
      builder: (context, child) {
        return Transform.translate(
          offset: Offset(_shakeAnimation.value, 0),
          child: child,
        );
      },
      child: widget.child,
    );
  }
}
```

---

## Disposing Controllers Properly

Failing to dispose controllers causes memory leaks and ticker-after-dispose errors.

### Rules

1. Always call `dispose()` on every `AnimationController` in the widget's `dispose()` method.
2. Call `super.dispose()` last.
3. If you add listeners with `addListener` or `addStatusListener`, they are cleaned up when the controller is disposed. You do not need to remove them manually unless you want to stop listening earlier.

```dart
@override
void dispose() {
  _controller1.dispose();
  _controller2.dispose();
  // Always call super.dispose() last
  super.dispose();
}
```

### Common Mistake: Triggering setState After Dispose

```dart
// BAD: This can call setState after the widget is disposed.
_controller.addStatusListener((status) {
  if (status == AnimationStatus.completed) {
    setState(() => _done = true); // Throws if widget is disposed
  }
});

// GOOD: Guard with mounted check.
_controller.addStatusListener((status) {
  if (status == AnimationStatus.completed && mounted) {
    setState(() => _done = true);
  }
});
```

---

## Using flutter_hooks: useAnimationController

The `flutter_hooks` package provides `useAnimationController`, which handles creation and disposal automatically. This eliminates boilerplate and the risk of forgetting to dispose.

Add the dependency:

```yaml
# pubspec.yaml
dependencies:
  flutter_hooks: ^0.21.0
```

### Basic Usage

```dart
import 'package:flutter/material.dart';
import 'package:flutter_hooks/flutter_hooks.dart';

class HookPulseAnimation extends HookWidget {
  const HookPulseAnimation({super.key});

  @override
  Widget build(BuildContext context) {
    final controller = useAnimationController(
      duration: const Duration(milliseconds: 600),
    )..repeat(reverse: true);

    return AnimatedBuilder(
      animation: controller,
      builder: (context, child) {
        return Transform.scale(
          scale: 0.8 + (controller.value * 0.4),
          child: child,
        );
      },
      child: Container(
        width: 100,
        height: 100,
        decoration: const BoxDecoration(
          color: Colors.red,
          shape: BoxShape.circle,
        ),
      ),
    );
  }
}
```

### With Tween and Curve

```dart
import 'package:flutter/material.dart';
import 'package:flutter_hooks/flutter_hooks.dart';

class HookSlideIn extends HookWidget {
  const HookSlideIn({super.key, required this.child});

  final Widget child;

  @override
  Widget build(BuildContext context) {
    final controller = useAnimationController(
      duration: const Duration(milliseconds: 500),
    );

    final slideAnimation = useMemoized(
      () => Tween<Offset>(
        begin: const Offset(0, 0.2),
        end: Offset.zero,
      ).animate(
        CurvedAnimation(parent: controller, curve: Curves.easeOutCubic),
      ),
      [controller],
    );

    final fadeAnimation = useMemoized(
      () => Tween<double>(begin: 0, end: 1).animate(
        CurvedAnimation(parent: controller, curve: Curves.easeIn),
      ),
      [controller],
    );

    useEffect(() {
      controller.forward();
      return null;
    }, const []);

    return AnimatedBuilder(
      animation: controller,
      builder: (context, child) {
        return FractionalTranslation(
          translation: slideAnimation.value,
          child: Opacity(
            opacity: fadeAnimation.value,
            child: child,
          ),
        );
      },
      child: child,
    );
  }
}
```

### Custom Hook for Repeated Patterns

```dart
import 'package:flutter/material.dart';
import 'package:flutter_hooks/flutter_hooks.dart';

/// Custom hook that creates a fade-in animation and triggers it on build.
Animation<double> useFadeIn({
  Duration duration = const Duration(milliseconds: 400),
  Curve curve = Curves.easeIn,
}) {
  final controller = useAnimationController(duration: duration);

  final animation = useMemoized(
    () => CurvedAnimation(parent: controller, curve: curve),
    [controller],
  );

  useEffect(() {
    controller.forward();
    return null;
  }, const []);

  return animation;
}

// Usage in a HookWidget:
class FadeInText extends HookWidget {
  const FadeInText({super.key, required this.text});

  final String text;

  @override
  Widget build(BuildContext context) {
    final opacity = useFadeIn(duration: const Duration(milliseconds: 600));

    return AnimatedBuilder(
      animation: opacity,
      builder: (context, child) {
        return Opacity(opacity: opacity.value, child: child);
      },
      child: Text(text, style: Theme.of(context).textTheme.headlineMedium),
    );
  }
}
```
