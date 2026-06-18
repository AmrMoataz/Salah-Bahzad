# Render Objects

Render objects are the lowest level of Flutter's rendering pipeline. They handle layout, painting, and hit testing directly. Most of the time, composing existing widgets is sufficient -- but when you need pixel-level control or custom layout algorithms, you drop down to this layer.

---

## When to Use CustomPainter vs RenderObject

| Use Case | Approach |
|---|---|
| Custom drawing (charts, shapes, effects) within standard layout | `CustomPainter` via `CustomPaint` |
| Custom layout algorithm (e.g., circular layout, flow layout) | Custom `RenderBox` or `RenderSliver` |
| Both custom layout and custom painting | Custom `RenderBox` (handles both) |
| Needs hit testing on custom-drawn regions | `CustomPainter` with `hitTest` override, or custom `RenderBox` |
| Performance-critical drawing that must avoid widget overhead | Custom `RenderBox` with `LeafRenderObjectWidget` |

**Rule of thumb:** Use `CustomPainter` when you only need to draw. Use a custom `RenderObject` when you need to control how children are sized and positioned.

---

## CustomPainter

`CustomPainter` lets you draw on a `Canvas` inside a widget that participates in normal layout.

```dart
class GaugeChart extends StatelessWidget {
  const GaugeChart({
    super.key,
    required this.value,
    required this.maxValue,
    this.size = 120,
  });

  final double value;
  final double maxValue;
  final double size;

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;

    return CustomPaint(
      size: Size(size, size),
      painter: _GaugePainter(
        value: value,
        maxValue: maxValue,
        trackColor: colorScheme.surfaceContainerHighest,
        fillColor: colorScheme.primary,
        textColor: colorScheme.onSurface,
      ),
    );
  }
}

class _GaugePainter extends CustomPainter {
  _GaugePainter({
    required this.value,
    required this.maxValue,
    required this.trackColor,
    required this.fillColor,
    required this.textColor,
  });

  final double value;
  final double maxValue;
  final Color trackColor;
  final Color fillColor;
  final Color textColor;

  static const double _startAngle = 2.3562; // 135 degrees in radians
  static const double _sweepAngle = 4.7124; // 270 degrees in radians

  @override
  void paint(Canvas canvas, Size size) {
    final center = Offset(size.width / 2, size.height / 2);
    final radius = size.width / 2 - 12;
    const strokeWidth = 10.0;

    // Draw track
    final trackPaint = Paint()
      ..color = trackColor
      ..style = PaintingStyle.stroke
      ..strokeWidth = strokeWidth
      ..strokeCap = StrokeCap.round;

    canvas.drawArc(
      Rect.fromCircle(center: center, radius: radius),
      _startAngle,
      _sweepAngle,
      false,
      trackPaint,
    );

    // Draw fill
    final fillFraction = (value / maxValue).clamp(0.0, 1.0);
    final fillPaint = Paint()
      ..color = fillColor
      ..style = PaintingStyle.stroke
      ..strokeWidth = strokeWidth
      ..strokeCap = StrokeCap.round;

    canvas.drawArc(
      Rect.fromCircle(center: center, radius: radius),
      _startAngle,
      _sweepAngle * fillFraction,
      false,
      fillPaint,
    );

    // Draw value text
    final textPainter = TextPainter(
      text: TextSpan(
        text: '${(fillFraction * 100).toInt()}%',
        style: TextStyle(
          color: textColor,
          fontSize: size.width * 0.2,
          fontWeight: FontWeight.bold,
        ),
      ),
      textDirection: TextDirection.ltr,
    )..layout();

    textPainter.paint(
      canvas,
      center - Offset(textPainter.width / 2, textPainter.height / 2),
    );
  }

  @override
  bool shouldRepaint(covariant _GaugePainter oldDelegate) {
    return value != oldDelegate.value ||
        maxValue != oldDelegate.maxValue ||
        fillColor != oldDelegate.fillColor;
  }

  @override
  bool hitTest(Offset position) {
    // Only respond to taps within the circular area
    final center = Offset(120 / 2, 120 / 2);
    return (position - center).distance <= 60;
  }
}
```

### CustomPainter with RepaintBoundary

When a `CustomPainter` animates, wrap it in a `RepaintBoundary` so repaints do not propagate up the tree.

```dart
class AnimatedWave extends StatefulWidget {
  const AnimatedWave({super.key});

  @override
  State<AnimatedWave> createState() => _AnimatedWaveState();
}

class _AnimatedWaveState extends State<AnimatedWave>
    with SingleTickerProviderStateMixin {
  late final AnimationController _controller;

  @override
  void initState() {
    super.initState();
    _controller = AnimationController(
      vsync: this,
      duration: const Duration(seconds: 2),
    )..repeat();
  }

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return RepaintBoundary(
      child: AnimatedBuilder(
        animation: _controller,
        builder: (context, child) {
          return CustomPaint(
            size: const Size(double.infinity, 100),
            painter: _WavePainter(
              phase: _controller.value * 2 * 3.14159265,
              color: Theme.of(context).colorScheme.primary,
            ),
          );
        },
      ),
    );
  }
}

class _WavePainter extends CustomPainter {
  _WavePainter({required this.phase, required this.color});

  final double phase;
  final Color color;

  @override
  void paint(Canvas canvas, Size size) {
    final paint = Paint()
      ..color = color.withValues(alpha: 0.3)
      ..style = PaintingStyle.fill;

    final path = Path()..moveTo(0, size.height);

    for (double x = 0; x <= size.width; x++) {
      final y = size.height * 0.5 +
          20 * _sin((x / size.width * 2 * 3.14159265) + phase);
      path.lineTo(x, y);
    }

    path
      ..lineTo(size.width, size.height)
      ..close();

    canvas.drawPath(path, paint);
  }

  static double _sin(double radians) {
    // Using dart:math sin in real code
    // Simplified inline for demonstration
    return radians - (radians * radians * radians) / 6;
  }

  @override
  bool shouldRepaint(covariant _WavePainter oldDelegate) {
    return phase != oldDelegate.phase || color != oldDelegate.color;
  }
}
```

---

## Creating a Basic RenderBox

A `RenderBox` handles layout (sizing and positioning) and painting for a node in the render tree. You create one via a `LeafRenderObjectWidget` (no children), `SingleChildRenderObjectWidget` (one child), or `MultiChildRenderObjectWidget` (multiple children).

### LeafRenderObjectWidget -- no children

```dart
/// A colored circle that sizes itself based on parent constraints.
class ColoredCircle extends LeafRenderObjectWidget {
  const ColoredCircle({super.key, required this.color, this.preferredSize = 48});

  final Color color;
  final double preferredSize;

  @override
  RenderObject createRenderObject(BuildContext context) {
    return RenderColoredCircle(color: color, preferredSize: preferredSize);
  }

  @override
  void updateRenderObject(BuildContext context, RenderColoredCircle renderObject) {
    renderObject
      ..color = color
      ..preferredSize = preferredSize;
  }
}

class RenderColoredCircle extends RenderBox {
  RenderColoredCircle({required Color color, required double preferredSize})
      : _color = color,
        _preferredSize = preferredSize;

  Color _color;
  Color get color => _color;
  set color(Color value) {
    if (_color == value) return;
    _color = value;
    markNeedsPaint(); // Only repaint, no re-layout needed
  }

  double _preferredSize;
  double get preferredSize => _preferredSize;
  set preferredSize(double value) {
    if (_preferredSize == value) return;
    _preferredSize = value;
    markNeedsLayout(); // Size changed, need re-layout
  }

  @override
  void performLayout() {
    // Respect constraints: try to be preferredSize, but clamp to constraints
    size = constraints.constrain(Size(preferredSize, preferredSize));
  }

  @override
  void paint(PaintingContext context, Offset offset) {
    final paint = Paint()..color = _color;
    final center = offset + Offset(size.width / 2, size.height / 2);
    final radius = size.shortestSide / 2;
    context.canvas.drawCircle(center, radius, paint);
  }

  @override
  bool hitTestSelf(Offset position) {
    // Only hit-test within the circle
    final center = Offset(size.width / 2, size.height / 2);
    return (position - center).distance <= size.shortestSide / 2;
  }
}
```

### SingleChildRenderObjectWidget -- one child

```dart
/// A widget that adds a custom border and padding around its child.
class FancyBorder extends SingleChildRenderObjectWidget {
  const FancyBorder({
    super.key,
    super.child,
    required this.borderColor,
    this.borderWidth = 2.0,
    this.internalPadding = 8.0,
  });

  final Color borderColor;
  final double borderWidth;
  final double internalPadding;

  @override
  RenderObject createRenderObject(BuildContext context) {
    return RenderFancyBorder(
      borderColor: borderColor,
      borderWidth: borderWidth,
      internalPadding: internalPadding,
    );
  }

  @override
  void updateRenderObject(BuildContext context, RenderFancyBorder renderObject) {
    renderObject
      ..borderColor = borderColor
      ..borderWidth = borderWidth
      ..internalPadding = internalPadding;
  }
}

class RenderFancyBorder extends RenderProxyBox {
  RenderFancyBorder({
    required Color borderColor,
    required double borderWidth,
    required double internalPadding,
  })  : _borderColor = borderColor,
        _borderWidth = borderWidth,
        _internalPadding = internalPadding;

  Color _borderColor;
  Color get borderColor => _borderColor;
  set borderColor(Color value) {
    if (_borderColor == value) return;
    _borderColor = value;
    markNeedsPaint();
  }

  double _borderWidth;
  double get borderWidth => _borderWidth;
  set borderWidth(double value) {
    if (_borderWidth == value) return;
    _borderWidth = value;
    markNeedsLayout();
  }

  double _internalPadding;
  double get internalPadding => _internalPadding;
  set internalPadding(double value) {
    if (_internalPadding == value) return;
    _internalPadding = value;
    markNeedsLayout();
  }

  double get _totalInset => _borderWidth + _internalPadding;

  @override
  void performLayout() {
    if (child != null) {
      final innerConstraints = constraints.deflate(
        EdgeInsets.all(_totalInset),
      );
      child!.layout(innerConstraints, parentUsesSize: true);
      size = constraints.constrain(Size(
        child!.size.width + _totalInset * 2,
        child!.size.height + _totalInset * 2,
      ));
    } else {
      size = constraints.constrain(Size(_totalInset * 2, _totalInset * 2));
    }
  }

  @override
  void paint(PaintingContext context, Offset offset) {
    // Paint the border
    final borderPaint = Paint()
      ..color = _borderColor
      ..style = PaintingStyle.stroke
      ..strokeWidth = _borderWidth;

    final borderRect = Rect.fromLTWH(
      offset.dx + _borderWidth / 2,
      offset.dy + _borderWidth / 2,
      size.width - _borderWidth,
      size.height - _borderWidth,
    );

    context.canvas.drawRRect(
      RRect.fromRectAndRadius(borderRect, const Radius.circular(8)),
      borderPaint,
    );

    // Paint the child offset by the inset
    if (child != null) {
      context.paintChild(child!, offset + Offset(_totalInset, _totalInset));
    }
  }
}
```

---

## Layout Protocol

The layout protocol follows a strict parent-to-child flow:

1. **Parent calls `child.layout(constraints, parentUsesSize: true)`** -- passing `BoxConstraints`.
2. **Child determines its own size in `performLayout()`** -- within the given constraints.
3. **Parent reads `child.size`** (only if `parentUsesSize: true`) and positions the child by setting `child.parentData`.

### performLayout

```dart
@override
void performLayout() {
  // For a leaf node -- choose a size within constraints
  size = constraints.constrain(const Size(100, 50));

  // For a parent with one child
  if (child != null) {
    child!.layout(constraints, parentUsesSize: true);
    size = child!.size;
  }

  // For a parent with multiple children (using ContainerRenderObjectMixin)
  // Lay out children sequentially, stacking vertically:
  double yOffset = 0;
  RenderBox? currentChild = firstChild;
  while (currentChild != null) {
    currentChild.layout(
      BoxConstraints(maxWidth: constraints.maxWidth),
      parentUsesSize: true,
    );
    final childParentData = currentChild.parentData! as BoxParentData;
    childParentData.offset = Offset(0, yOffset);
    yOffset += currentChild.size.height;
    currentChild = childAfter(currentChild);
  }
  size = constraints.constrain(Size(constraints.maxWidth, yOffset));
}
```

### Intrinsic Dimensions

Intrinsic dimensions let parent widgets query how large a child would like to be before committing to layout. These are used by widgets like `IntrinsicWidth` and `IntrinsicHeight`.

```dart
@override
double computeMinIntrinsicWidth(double height) {
  // The minimum width this render object needs to paint itself,
  // given an infinite height (or the provided height constraint).
  // For a circle, min width equals the diameter.
  return preferredSize;
}

@override
double computeMaxIntrinsicWidth(double height) {
  // The width beyond which additional space is wasted.
  return preferredSize;
}

@override
double computeMinIntrinsicHeight(double width) {
  return preferredSize;
}

@override
double computeMaxIntrinsicHeight(double width) {
  return preferredSize;
}
```

### computeDryLayout

`computeDryLayout` lets the framework query what size the render object would be for given constraints, without actually performing layout. This is used for intrinsic sizing and animations.

```dart
@override
Size computeDryLayout(BoxConstraints constraints) {
  // Return what size we would be without side effects
  return constraints.constrain(Size(preferredSize, preferredSize));
}
```

---

## Paint Protocol

Painting happens in `paint(PaintingContext context, Offset offset)`. The `offset` is the position of this render object relative to its parent.

### Key rules

1. Always offset all drawing commands by `offset`.
2. Paint children using `context.paintChild(child, childOffset)`.
3. Use `context.pushClipRect`, `context.pushOpacity`, etc. for effects.
4. Mark repaints with `markNeedsPaint()` -- never call `paint` directly.

```dart
@override
void paint(PaintingContext context, Offset offset) {
  final canvas = context.canvas;

  // Draw background
  canvas.drawRect(
    offset & size, // Shorthand for Rect.fromLTWH(offset.dx, offset.dy, size.width, size.height)
    Paint()..color = const Color(0xFFF5F5F5),
  );

  // Draw shadow
  final shadowPath = Path()
    ..addRRect(RRect.fromRectAndRadius(
      (offset & size).deflate(4),
      const Radius.circular(8),
    ));
  canvas.drawShadow(shadowPath, const Color(0x33000000), 4, false);

  // Paint children with clipping
  if (child != null) {
    context.pushClipRRect(
      needsCompositing,
      offset,
      offset & size,
      RRect.fromRectAndRadius(offset & size, const Radius.circular(8)),
      (context, offset) {
        context.paintChild(child!, offset);
      },
    );
  }
}
```

### Custom compositing with layers

For advanced effects like opacity or transform, push layers onto the compositing tree:

```dart
@override
void paint(PaintingContext context, Offset offset) {
  // Apply opacity to the entire subtree
  if (_opacity < 1.0) {
    context.pushOpacity(
      offset,
      (_opacity * 255).round(),
      (context, offset) {
        _paintContent(context, offset);
      },
    );
  } else {
    _paintContent(context, offset);
  }
}

void _paintContent(PaintingContext context, Offset offset) {
  // Paint actual content here
  if (child != null) {
    context.paintChild(child!, offset);
  }
}
```

---

## Hit Testing

Hit testing determines which render object receives a pointer event. Flutter walks the render tree from the root, calling `hitTest` on each node.

### hitTestSelf

Return `true` if this render object should absorb the hit (even without children).

```dart
@override
bool hitTestSelf(Offset position) {
  // Accept hits anywhere within bounds
  return true;
}
```

### hitTestChildren

Forward hit testing to children. For `RenderBox` with `ContainerRenderObjectMixin`:

```dart
@override
bool hitTestChildren(BoxHitTestResult result, {required Offset position}) {
  // Test children in reverse paint order (last painted = first to receive events)
  RenderBox? child = lastChild;
  while (child != null) {
    final childParentData = child.parentData! as BoxParentData;
    final isHit = result.addWithPaintOffset(
      offset: childParentData.offset,
      position: position,
      hitTest: (result, transformed) {
        return child!.hitTest(result, position: transformed);
      },
    );
    if (isHit) return true;
    child = childBefore(child);
  }
  return false;
}
```

### Full hit test example with custom shape

```dart
class RenderHexagon extends RenderBox {
  RenderHexagon({required Color color}) : _color = color;

  Color _color;
  Color get color => _color;
  set color(Color value) {
    if (_color == value) return;
    _color = value;
    markNeedsPaint();
  }

  late Path _hexPath;

  @override
  void performLayout() {
    size = constraints.constrain(const Size(100, 100));
    _hexPath = _buildHexagonPath(size);
  }

  Path _buildHexagonPath(Size size) {
    final center = Offset(size.width / 2, size.height / 2);
    final radius = size.shortestSide / 2;
    final path = Path();

    for (int i = 0; i < 6; i++) {
      final angle = (i * 60 - 30) * 3.14159265 / 180;
      final point = Offset(
        center.dx + radius * _cos(angle),
        center.dy + radius * _sin(angle),
      );
      if (i == 0) {
        path.moveTo(point.dx, point.dy);
      } else {
        path.lineTo(point.dx, point.dy);
      }
    }
    path.close();
    return path;
  }

  static double _cos(double r) => 1 - r * r / 2 + r * r * r * r / 24;
  static double _sin(double r) => r - r * r * r / 6 + r * r * r * r * r / 120;

  @override
  void paint(PaintingContext context, Offset offset) {
    context.canvas.save();
    context.canvas.translate(offset.dx, offset.dy);
    context.canvas.drawPath(_hexPath, Paint()..color = _color);
    context.canvas.restore();
  }

  @override
  bool hitTestSelf(Offset position) {
    // Only register hits inside the hexagon path
    return _hexPath.contains(position);
  }
}

// Widget wrapper
class Hexagon extends LeafRenderObjectWidget {
  const Hexagon({super.key, required this.color});

  final Color color;

  @override
  RenderObject createRenderObject(BuildContext context) {
    return RenderHexagon(color: color);
  }

  @override
  void updateRenderObject(BuildContext context, RenderHexagon renderObject) {
    renderObject.color = color;
  }
}
```

### GestureDetector integration

To handle gestures on a custom render object, wrap the widget with `GestureDetector` or `Listener`. The render object's `hitTestSelf` determines whether the gesture is recognized.

```dart
GestureDetector(
  onTap: () => debugPrint('Hexagon tapped!'),
  child: const Hexagon(color: Colors.teal),
)
```

Because `RenderHexagon.hitTestSelf` only returns `true` when the tap is inside the hexagonal path, taps outside the hexagon (but inside the bounding box) are ignored.
