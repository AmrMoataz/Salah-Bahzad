# Implicit Animations

Implicit animations are the simplest way to animate in Flutter. You change a property value, provide a duration, and Flutter interpolates between the old and new values automatically. No `AnimationController` required.

---

## AnimatedContainer

The most versatile implicit animation widget. It animates changes to its decoration, dimensions, padding, margin, alignment, and transform.

```dart
import 'package:flutter/material.dart';

class AnimatedContainerExample extends StatefulWidget {
  const AnimatedContainerExample({super.key});

  @override
  State<AnimatedContainerExample> createState() =>
      _AnimatedContainerExampleState();
}

class _AnimatedContainerExampleState extends State<AnimatedContainerExample> {
  bool _expanded = false;

  @override
  Widget build(BuildContext context) {
    return GestureDetector(
      onTap: () => setState(() => _expanded = !_expanded),
      child: AnimatedContainer(
        duration: const Duration(milliseconds: 300),
        curve: Curves.easeInOut,
        width: _expanded ? 200 : 100,
        height: _expanded ? 200 : 100,
        decoration: BoxDecoration(
          color: _expanded ? Colors.blue : Colors.red,
          borderRadius: BorderRadius.circular(_expanded ? 32 : 8),
          boxShadow: [
            BoxShadow(
              color: Colors.black.withValues(alpha: 0.3),
              blurRadius: _expanded ? 16 : 4,
              offset: Offset(0, _expanded ? 8 : 2),
            ),
          ],
        ),
        alignment: _expanded ? Alignment.center : Alignment.topLeft,
        padding: EdgeInsets.all(_expanded ? 24 : 8),
        child: const Text(
          'Tap me',
          style: TextStyle(color: Colors.white, fontWeight: FontWeight.bold),
        ),
      ),
    );
  }
}
```

---

## AnimatedOpacity

Fades a widget in or out by animating its opacity.

```dart
import 'package:flutter/material.dart';

class AnimatedOpacityExample extends StatefulWidget {
  const AnimatedOpacityExample({super.key});

  @override
  State<AnimatedOpacityExample> createState() => _AnimatedOpacityExampleState();
}

class _AnimatedOpacityExampleState extends State<AnimatedOpacityExample> {
  bool _visible = true;

  @override
  Widget build(BuildContext context) {
    return Column(
      mainAxisAlignment: MainAxisAlignment.center,
      children: [
        AnimatedOpacity(
          opacity: _visible ? 1.0 : 0.0,
          duration: const Duration(milliseconds: 400),
          curve: Curves.easeIn,
          child: const FlutterLogo(size: 80),
        ),
        const SizedBox(height: 24),
        ElevatedButton(
          onPressed: () => setState(() => _visible = !_visible),
          child: Text(_visible ? 'Hide' : 'Show'),
        ),
      ],
    );
  }
}
```

---

## AnimatedPadding

Animates changes to padding around a child widget.

```dart
import 'package:flutter/material.dart';

class AnimatedPaddingExample extends StatefulWidget {
  const AnimatedPaddingExample({super.key});

  @override
  State<AnimatedPaddingExample> createState() => _AnimatedPaddingExampleState();
}

class _AnimatedPaddingExampleState extends State<AnimatedPaddingExample> {
  bool _padded = false;

  @override
  Widget build(BuildContext context) {
    return GestureDetector(
      onTap: () => setState(() => _padded = !_padded),
      child: Container(
        color: Colors.grey.shade200,
        child: AnimatedPadding(
          padding: EdgeInsets.all(_padded ? 48.0 : 8.0),
          duration: const Duration(milliseconds: 300),
          curve: Curves.easeOutCubic,
          child: Container(
            color: Colors.teal,
            width: 100,
            height: 100,
            alignment: Alignment.center,
            child: const Text(
              'Tap',
              style: TextStyle(color: Colors.white),
            ),
          ),
        ),
      ),
    );
  }
}
```

---

## AnimatedAlign

Animates the alignment of a child within its parent.

```dart
import 'package:flutter/material.dart';

class AnimatedAlignExample extends StatefulWidget {
  const AnimatedAlignExample({super.key});

  @override
  State<AnimatedAlignExample> createState() => _AnimatedAlignExampleState();
}

class _AnimatedAlignExampleState extends State<AnimatedAlignExample> {
  bool _alignedRight = false;

  @override
  Widget build(BuildContext context) {
    return Column(
      children: [
        SizedBox(
          height: 120,
          child: AnimatedAlign(
            alignment:
                _alignedRight ? Alignment.centerRight : Alignment.centerLeft,
            duration: const Duration(milliseconds: 400),
            curve: Curves.elasticOut,
            child: Container(
              width: 60,
              height: 60,
              decoration: const BoxDecoration(
                color: Colors.deepPurple,
                shape: BoxShape.circle,
              ),
            ),
          ),
        ),
        ElevatedButton(
          onPressed: () => setState(() => _alignedRight = !_alignedRight),
          child: const Text('Toggle alignment'),
        ),
      ],
    );
  }
}
```

---

## AnimatedPositioned

Animates a widget's position inside a `Stack`. All four directional properties (`top`, `left`, `right`, `bottom`) and `width`/`height` are animatable.

```dart
import 'package:flutter/material.dart';

class AnimatedPositionedExample extends StatefulWidget {
  const AnimatedPositionedExample({super.key});

  @override
  State<AnimatedPositionedExample> createState() =>
      _AnimatedPositionedExampleState();
}

class _AnimatedPositionedExampleState extends State<AnimatedPositionedExample> {
  bool _moved = false;

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      width: 300,
      height: 300,
      child: Stack(
        children: [
          AnimatedPositioned(
            duration: const Duration(milliseconds: 500),
            curve: Curves.fastOutSlowIn,
            left: _moved ? 200 : 20,
            top: _moved ? 200 : 20,
            width: _moved ? 80 : 60,
            height: _moved ? 80 : 60,
            child: GestureDetector(
              onTap: () => setState(() => _moved = !_moved),
              child: Container(
                decoration: BoxDecoration(
                  color: Colors.orange,
                  borderRadius: BorderRadius.circular(12),
                ),
                alignment: Alignment.center,
                child: const Icon(Icons.touch_app, color: Colors.white),
              ),
            ),
          ),
        ],
      ),
    );
  }
}
```

---

## AnimatedDefaultTextStyle

Animates text style changes including font size, weight, color, and letter spacing.

```dart
import 'package:flutter/material.dart';

class AnimatedTextStyleExample extends StatefulWidget {
  const AnimatedTextStyleExample({super.key});

  @override
  State<AnimatedTextStyleExample> createState() =>
      _AnimatedTextStyleExampleState();
}

class _AnimatedTextStyleExampleState extends State<AnimatedTextStyleExample> {
  bool _highlighted = false;

  @override
  Widget build(BuildContext context) {
    return GestureDetector(
      onTap: () => setState(() => _highlighted = !_highlighted),
      child: AnimatedDefaultTextStyle(
        duration: const Duration(milliseconds: 300),
        curve: Curves.easeInOut,
        style: _highlighted
            ? const TextStyle(
                fontSize: 32,
                fontWeight: FontWeight.bold,
                color: Colors.deepOrange,
                letterSpacing: 2,
              )
            : const TextStyle(
                fontSize: 18,
                fontWeight: FontWeight.normal,
                color: Colors.black87,
                letterSpacing: 0,
              ),
        child: const Text('Tap to highlight'),
      ),
    );
  }
}
```

---

## AnimatedSwitcher

Cross-fades between an old child and a new child when the child's key or type changes.

```dart
import 'package:flutter/material.dart';

class AnimatedSwitcherExample extends StatefulWidget {
  const AnimatedSwitcherExample({super.key});

  @override
  State<AnimatedSwitcherExample> createState() =>
      _AnimatedSwitcherExampleState();
}

class _AnimatedSwitcherExampleState extends State<AnimatedSwitcherExample> {
  int _count = 0;

  @override
  Widget build(BuildContext context) {
    return Column(
      mainAxisAlignment: MainAxisAlignment.center,
      children: [
        AnimatedSwitcher(
          duration: const Duration(milliseconds: 300),
          transitionBuilder: (child, animation) {
            return ScaleTransition(
              scale: animation,
              child: FadeTransition(opacity: animation, child: child),
            );
          },
          child: Text(
            '$_count',
            // Key is essential: AnimatedSwitcher uses it to detect changes.
            key: ValueKey<int>(_count),
            style: const TextStyle(fontSize: 48, fontWeight: FontWeight.bold),
          ),
        ),
        const SizedBox(height: 24),
        ElevatedButton(
          onPressed: () => setState(() => _count++),
          child: const Text('Increment'),
        ),
      ],
    );
  }
}
```

### Custom Layout for AnimatedSwitcher

Override the default `Stack` layout to use a single-child layout:

```dart
AnimatedSwitcher(
  duration: const Duration(milliseconds: 250),
  layoutBuilder: (currentChild, previousChildren) {
    return currentChild ?? const SizedBox.shrink();
  },
  child: _showFirst
      ? const Icon(Icons.check, key: ValueKey('check'), size: 48)
      : const Icon(Icons.close, key: ValueKey('close'), size: 48),
)
```

---

## TweenAnimationBuilder

Build custom implicit animations for any value type. Use this when no built-in implicit widget exists for the property you want to animate.

```dart
import 'package:flutter/material.dart';

class TweenAnimationBuilderExample extends StatefulWidget {
  const TweenAnimationBuilderExample({super.key});

  @override
  State<TweenAnimationBuilderExample> createState() =>
      _TweenAnimationBuilderExampleState();
}

class _TweenAnimationBuilderExampleState
    extends State<TweenAnimationBuilderExample> {
  double _targetBorderRadius = 8;

  @override
  Widget build(BuildContext context) {
    return GestureDetector(
      onTap: () {
        setState(() {
          _targetBorderRadius = _targetBorderRadius == 8 ? 48 : 8;
        });
      },
      child: TweenAnimationBuilder<double>(
        tween: Tween<double>(end: _targetBorderRadius),
        duration: const Duration(milliseconds: 400),
        curve: Curves.easeOutBack,
        builder: (context, radius, child) {
          return Container(
            width: 120,
            height: 120,
            decoration: BoxDecoration(
              color: Colors.indigo,
              borderRadius: BorderRadius.circular(radius),
            ),
            alignment: Alignment.center,
            child: child,
          );
        },
        // The child parameter is an optimization: this widget does not rebuild.
        child: const Icon(Icons.animation, color: Colors.white, size: 40),
      ),
    );
  }
}
```

### TweenAnimationBuilder with Color

```dart
TweenAnimationBuilder<Color?>(
  tween: ColorTween(
    begin: Colors.blue,
    end: _isActive ? Colors.green : Colors.blue,
  ),
  duration: const Duration(milliseconds: 350),
  builder: (context, color, child) {
    return Container(
      width: 100,
      height: 100,
      color: color,
      child: child,
    );
  },
  child: const Center(
    child: Text('Color', style: TextStyle(color: Colors.white)),
  ),
)
```

---

## AnimatedCrossFade

Toggles between two widgets with a cross-fade and size animation.

```dart
import 'package:flutter/material.dart';

class AnimatedCrossFadeExample extends StatefulWidget {
  const AnimatedCrossFadeExample({super.key});

  @override
  State<AnimatedCrossFadeExample> createState() =>
      _AnimatedCrossFadeExampleState();
}

class _AnimatedCrossFadeExampleState extends State<AnimatedCrossFadeExample> {
  bool _showFirst = true;

  @override
  Widget build(BuildContext context) {
    return Column(
      mainAxisAlignment: MainAxisAlignment.center,
      children: [
        AnimatedCrossFade(
          firstChild: Container(
            width: 200,
            height: 100,
            color: Colors.blue,
            alignment: Alignment.center,
            child: const Text(
              'First Widget',
              style: TextStyle(color: Colors.white, fontSize: 18),
            ),
          ),
          secondChild: Container(
            width: 200,
            height: 200,
            color: Colors.green,
            alignment: Alignment.center,
            child: const Text(
              'Second Widget',
              style: TextStyle(color: Colors.white, fontSize: 18),
            ),
          ),
          crossFadeState: _showFirst
              ? CrossFadeState.showFirst
              : CrossFadeState.showSecond,
          duration: const Duration(milliseconds: 300),
          firstCurve: Curves.easeIn,
          secondCurve: Curves.easeOut,
          sizeCurve: Curves.easeInOut,
        ),
        const SizedBox(height: 24),
        ElevatedButton(
          onPressed: () => setState(() => _showFirst = !_showFirst),
          child: const Text('Toggle'),
        ),
      ],
    );
  }
}
```

---

## Duration and Curve Selection Guide

### Duration Guidelines

| Animation Type | Recommended Duration | Rationale |
|---|---|---|
| Micro-interaction (button press, toggle) | 100 - 200ms | Must feel instant and responsive |
| Property transition (color, size, opacity) | 200 - 350ms | Noticeable but not sluggish |
| Layout change (expand, collapse, reorder) | 250 - 400ms | Complex visual change needs time to read |
| Page/route transition | 300 - 500ms | Matches platform conventions |
| Emphasis animation (attention grabber) | 400 - 800ms | Deliberate, noticeable motion |
| Ambient / looping | 1000ms+ | Background, non-blocking motion |

### Curve Reference

| Curve | Behavior | Best For |
|---|---|---|
| `Curves.linear` | Constant speed | Progress indicators, mechanical motion |
| `Curves.easeIn` | Slow start, fast end | Elements leaving the screen |
| `Curves.easeOut` | Fast start, slow end | Elements entering the screen |
| `Curves.easeInOut` | Slow start and end | General-purpose UI transitions |
| `Curves.fastOutSlowIn` | Material Design standard | Default for most Material animations |
| `Curves.easeOutCubic` | Smooth deceleration | Cards, sheets sliding in |
| `Curves.easeOutBack` | Overshoots then settles | Playful entrances, emphasis |
| `Curves.elasticOut` | Springy overshoot | Bouncing elements, attention-grabbing |
| `Curves.bounceOut` | Bounces at end | Dropping/gravity-like effects |
| `Curves.decelerate` | Fast start, very slow end | Fling/scroll deceleration |

### Custom Curves

```dart
// Create a cubic bezier curve matching CSS cubic-bezier(0.4, 0.0, 0.2, 1.0)
const customCurve = Cubic(0.4, 0.0, 0.2, 1.0);

// Use in any animation
AnimatedContainer(
  duration: const Duration(milliseconds: 300),
  curve: customCurve,
  // ...
)
```

---

## When to Use Implicit vs Explicit

**Use implicit animations when:**
- You are animating a single property or a small set of properties on one widget.
- The animation is fire-and-forget (you do not need to play, pause, reverse, or loop).
- You want the simplest possible code.
- The animation is triggered by a state change (e.g., a boolean toggle).

**Use explicit animations when:**
- You need to control the animation lifecycle (play, pause, stop, reverse, repeat).
- You need to chain multiple animations in sequence.
- You need staggered timing across multiple widgets.
- You need to respond to animation status (completed, dismissed).
- You need to drive the animation from a gesture (drag, swipe).
- The animation loops continuously.
