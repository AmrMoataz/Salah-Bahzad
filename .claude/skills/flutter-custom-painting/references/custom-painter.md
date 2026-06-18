# CustomPainter & Canvas API Reference

## Table of Contents

1. [CustomPaint Widget Setup](#custompaint-widget-setup)
2. [CustomPainter Class](#custompainter-class)
3. [Canvas Drawing API](#canvas-drawing-api)
4. [Paint Properties](#paint-properties)
5. [Path Operations](#path-operations)
6. [Path Boolean Operations](#path-boolean-operations)
7. [Clipping](#clipping)
8. [Text Painting](#text-painting)
9. [Drawing Charts](#drawing-charts)
10. [Performance Optimization](#performance-optimization)
11. [Touch Interaction](#touch-interaction)
12. [Animating Custom Painters](#animating-custom-painters)

---

## CustomPaint Widget Setup

`CustomPaint` is the widget that hosts a `CustomPainter`. It provides a canvas whose coordinate system originates at the top-left corner of the widget.

```dart
import 'package:flutter/material.dart';

class WaveBackground extends StatelessWidget {
  const WaveBackground({super.key});

  @override
  Widget build(BuildContext context) {
    return CustomPaint(
      // The painter draws behind child widgets.
      painter: WaveBackgroundPainter(
        color: Theme.of(context).colorScheme.primary,
      ),
      // foregroundPainter draws on top of child widgets.
      // foregroundPainter: ...,
      //
      // If there is no child the widget has zero size unless
      // you supply an explicit size.
      size: Size.infinite,
      child: const Center(
        child: Text('Hello, Custom Paint!'),
      ),
    );
  }
}
```

Key points:

- `painter` renders **behind** the child.
- `foregroundPainter` renders **in front of** the child.
- Without a `child`, set `size` explicitly or wrap in `SizedBox`.

---

## CustomPainter Class

Every painter extends `CustomPainter` and implements two methods.

```dart
class WaveBackgroundPainter extends CustomPainter {
  WaveBackgroundPainter({required this.color});

  final Color color;

  @override
  void paint(Canvas canvas, Size size) {
    final paint = Paint()
      ..color = color
      ..style = PaintingStyle.fill;

    final path = Path()
      ..moveTo(0, size.height * 0.75)
      ..quadraticBezierTo(
        size.width * 0.25,
        size.height * 0.65,
        size.width * 0.5,
        size.height * 0.75,
      )
      ..quadraticBezierTo(
        size.width * 0.75,
        size.height * 0.85,
        size.width,
        size.height * 0.75,
      )
      ..lineTo(size.width, size.height)
      ..lineTo(0, size.height)
      ..close();

    canvas.drawPath(path, paint);
  }

  @override
  bool shouldRepaint(covariant WaveBackgroundPainter oldDelegate) {
    return oldDelegate.color != color;
  }
}
```

### `paint(Canvas canvas, Size size)`

Called whenever the framework decides this painter needs to repaint. The `size` matches the layout size of the `CustomPaint` widget.

### `shouldRepaint(covariant T oldDelegate)`

Return `true` only when the visual output would change. Returning `true` unconditionally forces a repaint every frame -- avoid this in production.

---

## Canvas Drawing API

The `Canvas` class exposes numerous drawing primitives.

### drawLine

```dart
@override
void paint(Canvas canvas, Size size) {
  final paint = Paint()
    ..color = const Color(0xFF2196F3)
    ..strokeWidth = 3.0
    ..strokeCap = StrokeCap.round;

  canvas.drawLine(
    Offset(0, size.height / 2),
    Offset(size.width, size.height / 2),
    paint,
  );
}
```

### drawRect

```dart
@override
void paint(Canvas canvas, Size size) {
  final paint = Paint()
    ..color = const Color(0xFF4CAF50)
    ..style = PaintingStyle.stroke
    ..strokeWidth = 2.0;

  final rect = Rect.fromLTWH(20, 20, size.width - 40, size.height - 40);
  canvas.drawRect(rect, paint);
}
```

### drawRRect (rounded rectangle)

```dart
@override
void paint(Canvas canvas, Size size) {
  final paint = Paint()
    ..color = const Color(0xFFFF9800)
    ..style = PaintingStyle.fill;

  final rrect = RRect.fromRectAndRadius(
    Rect.fromLTWH(20, 20, size.width - 40, size.height - 40),
    const Radius.circular(16),
  );
  canvas.drawRRect(rrect, paint);
}
```

### drawCircle

```dart
@override
void paint(Canvas canvas, Size size) {
  final paint = Paint()
    ..color = const Color(0xFFE91E63)
    ..style = PaintingStyle.fill;

  final center = Offset(size.width / 2, size.height / 2);
  final radius = size.shortestSide / 3;
  canvas.drawCircle(center, radius, paint);
}
```

### drawArc

```dart
@override
void paint(Canvas canvas, Size size) {
  final paint = Paint()
    ..color = const Color(0xFF9C27B0)
    ..style = PaintingStyle.stroke
    ..strokeWidth = 6.0
    ..strokeCap = StrokeCap.round;

  final rect = Rect.fromCenter(
    center: Offset(size.width / 2, size.height / 2),
    width: 200,
    height: 200,
  );

  // Draw a 270-degree arc starting from the top (-pi/2).
  canvas.drawArc(
    rect,
    -3.14159 / 2, // startAngle
    3.14159 * 1.5, // sweepAngle (270 degrees)
    false, // useCenter
    paint,
  );
}
```

### drawPath

```dart
@override
void paint(Canvas canvas, Size size) {
  final paint = Paint()
    ..color = const Color(0xFF00BCD4)
    ..style = PaintingStyle.fill;

  final path = Path()
    ..moveTo(size.width / 2, 0)
    ..lineTo(size.width, size.height)
    ..lineTo(0, size.height)
    ..close();

  canvas.drawPath(path, paint);
}
```

### drawImage

Drawing a `dart:ui` `Image` requires decoding from bytes first.

```dart
import 'dart:ui' as ui;
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

class ImagePainter extends CustomPainter {
  ImagePainter({required this.image});

  final ui.Image image;

  @override
  void paint(Canvas canvas, Size size) {
    final srcRect = Rect.fromLTWH(
      0,
      0,
      image.width.toDouble(),
      image.height.toDouble(),
    );
    final dstRect = Rect.fromLTWH(0, 0, size.width, size.height);

    canvas.drawImageRect(image, srcRect, dstRect, Paint());
  }

  @override
  bool shouldRepaint(covariant ImagePainter oldDelegate) {
    return oldDelegate.image != image;
  }
}

/// Helper to decode an asset image into a dart:ui Image.
Future<ui.Image> loadUiImage(String assetPath) async {
  final data = await rootBundle.load(assetPath);
  final codec = await ui.instantiateImageCodec(data.buffer.asUint8List());
  final frame = await codec.getNextFrame();
  return frame.image;
}
```

---

## Paint Properties

The `Paint` object controls how shapes are rendered.

```dart
final paint = Paint()
  // Fill color.
  ..color = const Color(0xFF2196F3)

  // Fill vs stroke.
  ..style = PaintingStyle.fill   // or PaintingStyle.stroke

  // Stroke configuration (only applies when style == stroke).
  ..strokeWidth = 4.0
  ..strokeCap = StrokeCap.round   // butt, round, square
  ..strokeJoin = StrokeJoin.round // miter, round, bevel

  // Anti-aliasing (enabled by default).
  ..isAntiAlias = true

  // Blend mode for compositing.
  ..blendMode = BlendMode.srcOver

  // Color filter.
  ..colorFilter = const ColorFilter.mode(
    Color(0x80FF0000),
    BlendMode.srcATop,
  )

  // Mask filter (blur).
  ..maskFilter = const MaskFilter.blur(BlurStyle.normal, 8.0)

  // Shader (gradient, image shader, fragment shader).
  ..shader = const LinearGradient(
    colors: [Color(0xFF2196F3), Color(0xFF00BCD4)],
  ).createShader(const Rect.fromLTWH(0, 0, 300, 300));
```

### Gradient Shaders

```dart
// Linear gradient
final linearShader = const LinearGradient(
  begin: Alignment.topLeft,
  end: Alignment.bottomRight,
  colors: [Color(0xFFFF5722), Color(0xFFFFC107)],
).createShader(rect);

// Radial gradient
final radialShader = const RadialGradient(
  colors: [Color(0xFFE91E63), Color(0xFF9C27B0)],
).createShader(rect);

// Sweep gradient
final sweepShader = const SweepGradient(
  colors: [
    Color(0xFFFF5722),
    Color(0xFFFFC107),
    Color(0xFF4CAF50),
    Color(0xFF2196F3),
    Color(0xFFFF5722),
  ],
).createShader(rect);
```

---

## Path Operations

### Basic Path Construction

```dart
final path = Path()
  // Move the pen to an absolute position.
  ..moveTo(50, 100)

  // Draw a straight line to an absolute position.
  ..lineTo(150, 100)

  // Relative line (offset from current point).
  ..relativeLineTo(0, 80)

  // Cubic Bezier curve.
  ..cubicTo(
    100, 50,   // first control point
    200, 150,  // second control point
    250, 100,  // end point
  )

  // Quadratic Bezier curve.
  ..quadraticBezierTo(
    175, 0,    // control point
    300, 100,  // end point
  )

  // Arc from current point.
  ..arcToPoint(
    const Offset(350, 200),
    radius: const Radius.circular(50),
    clockwise: true,
  )

  // Close the subpath back to the last moveTo.
  ..close();
```

### Adding Shapes to a Path

```dart
final path = Path()
  ..addRect(const Rect.fromLTWH(10, 10, 100, 60))
  ..addOval(const Rect.fromLTWH(130, 10, 100, 60))
  ..addRRect(RRect.fromRectAndRadius(
    const Rect.fromLTWH(250, 10, 100, 60),
    const Radius.circular(12),
  ))
  ..addPolygon(
    [
      const Offset(50, 200),
      const Offset(100, 130),
      const Offset(150, 200),
    ],
    true, // close the polygon
  );
```

### Path Metrics

Use `PathMetric` to measure a path or extract sub-paths (useful for animations).

```dart
final metrics = path.computeMetrics();
for (final metric in metrics) {
  final totalLength = metric.length;

  // Extract the first half of the path.
  final halfPath = metric.extractPath(0, totalLength / 2);

  // Get the tangent at 40 % of the path.
  final tangent = metric.getTangentForOffset(totalLength * 0.4);
  if (tangent != null) {
    final position = tangent.position; // Offset
    final angle = tangent.angle; // radians
  }
}
```

---

## Path Boolean Operations

Combine two paths using `Path.combine`.

```dart
class BooleanOpsPainter extends CustomPainter {
  @override
  void paint(Canvas canvas, Size size) {
    final cx = size.width / 2;
    final cy = size.height / 2;

    final circlePath = Path()
      ..addOval(Rect.fromCircle(center: Offset(cx - 30, cy), radius: 60));

    final squarePath = Path()
      ..addRect(Rect.fromCenter(center: Offset(cx + 30, cy), width: 100, height: 100));

    // Available operations:
    //   PathOperation.difference
    //   PathOperation.intersect
    //   PathOperation.union
    //   PathOperation.reverseDifference
    //   PathOperation.xor
    final combined = Path.combine(
      PathOperation.intersect,
      circlePath,
      squarePath,
    );

    canvas.drawPath(
      combined,
      Paint()
        ..color = const Color(0xFF6200EA)
        ..style = PaintingStyle.fill,
    );
  }

  @override
  bool shouldRepaint(covariant BooleanOpsPainter oldDelegate) => false;
}
```

---

## Clipping

Canvas clipping restricts drawing to a region.

### clipRect

```dart
@override
void paint(Canvas canvas, Size size) {
  canvas.save();
  canvas.clipRect(
    Rect.fromLTWH(20, 20, size.width - 40, size.height - 40),
    doAntiAlias: true,
  );

  // Everything drawn after clipRect is confined to that rectangle.
  canvas.drawPaint(Paint()..color = const Color(0xFFE3F2FD));
  canvas.drawCircle(
    Offset(size.width / 2, size.height / 2),
    size.width,
    Paint()..color = const Color(0xFF1565C0),
  );

  canvas.restore();
}
```

### clipRRect

```dart
canvas.save();
canvas.clipRRect(
  RRect.fromRectAndRadius(
    Rect.fromLTWH(16, 16, size.width - 32, size.height - 32),
    const Radius.circular(24),
  ),
);
// ...draw operations...
canvas.restore();
```

### clipPath

```dart
canvas.save();
final starPath = _buildStarPath(size);
canvas.clipPath(starPath);

// Draw an image or gradient that is masked by the star shape.
canvas.drawPaint(
  Paint()
    ..shader = const LinearGradient(
      colors: [Color(0xFFFF5722), Color(0xFFFFC107)],
    ).createShader(Offset.zero & size),
);
canvas.restore();
```

Utility to build a star path:

```dart
Path _buildStarPath(Size size) {
  const points = 5;
  final cx = size.width / 2;
  final cy = size.height / 2;
  final outerR = size.shortestSide / 2;
  final innerR = outerR * 0.4;
  final path = Path();

  for (var i = 0; i < points * 2; i++) {
    final radius = i.isEven ? outerR : innerR;
    final angle = (i * 3.14159 / points) - (3.14159 / 2);
    final x = cx + radius * _cos(angle);
    final y = cy + radius * _sin(angle);
    if (i == 0) {
      path.moveTo(x, y);
    } else {
      path.lineTo(x, y);
    }
  }
  path.close();
  return path;
}

double _cos(double radians) => radians.cos(); // Use dart:math cos
double _sin(double radians) => radians.sin(); // Use dart:math sin
```

> **Note:** In real code, import `dart:math` and use `cos(angle)` / `sin(angle)` from that library.

Complete star-clipped painter with correct imports:

```dart
import 'dart:math' as math;
import 'package:flutter/material.dart';

class StarClipPainter extends CustomPainter {
  @override
  void paint(Canvas canvas, Size size) {
    final path = _starPath(size, points: 5);

    canvas.save();
    canvas.clipPath(path);

    canvas.drawPaint(
      Paint()
        ..shader = const LinearGradient(
          colors: [Color(0xFFFF5722), Color(0xFFFFC107)],
        ).createShader(Offset.zero & size),
    );
    canvas.restore();
  }

  Path _starPath(Size size, {required int points}) {
    final cx = size.width / 2;
    final cy = size.height / 2;
    final outerR = size.shortestSide / 2;
    final innerR = outerR * 0.4;
    final path = Path();

    for (var i = 0; i < points * 2; i++) {
      final radius = i.isEven ? outerR : innerR;
      final angle = (i * math.pi / points) - (math.pi / 2);
      final x = cx + radius * math.cos(angle);
      final y = cy + radius * math.sin(angle);
      if (i == 0) {
        path.moveTo(x, y);
      } else {
        path.lineTo(x, y);
      }
    }
    path.close();
    return path;
  }

  @override
  bool shouldRepaint(covariant StarClipPainter oldDelegate) => false;
}
```

---

## Text Painting

Use `TextPainter` to render text on the canvas.

```dart
import 'package:flutter/material.dart';

class LabelPainter extends CustomPainter {
  LabelPainter({
    required this.label,
    required this.textStyle,
  });

  final String label;
  final TextStyle textStyle;

  @override
  void paint(Canvas canvas, Size size) {
    final textSpan = TextSpan(text: label, style: textStyle);
    final textPainter = TextPainter(
      text: textSpan,
      textDirection: TextDirection.ltr,
      textAlign: TextAlign.center,
    )..layout(maxWidth: size.width);

    // Center the text.
    final offset = Offset(
      (size.width - textPainter.width) / 2,
      (size.height - textPainter.height) / 2,
    );
    textPainter.paint(canvas, offset);
  }

  @override
  bool shouldRepaint(covariant LabelPainter oldDelegate) {
    return oldDelegate.label != label || oldDelegate.textStyle != textStyle;
  }
}
```

### Multiline and Rich Text

```dart
@override
void paint(Canvas canvas, Size size) {
  final span = TextSpan(
    children: [
      TextSpan(
        text: 'Revenue: ',
        style: TextStyle(
          color: const Color(0xFF424242),
          fontSize: 14,
          fontWeight: FontWeight.w400,
        ),
      ),
      TextSpan(
        text: '\$12,400',
        style: TextStyle(
          color: const Color(0xFF2E7D32),
          fontSize: 14,
          fontWeight: FontWeight.w700,
        ),
      ),
    ],
  );

  final tp = TextPainter(
    text: span,
    textDirection: TextDirection.ltr,
    maxLines: 2,
    ellipsis: '...',
  )..layout(maxWidth: size.width - 32);

  tp.paint(canvas, const Offset(16, 16));
}
```

---

## Drawing Charts

### Bar Chart

A complete, self-contained bar chart widget.

```dart
import 'dart:math' as math;
import 'package:flutter/material.dart';

/// A single bar in the chart.
class BarData {
  const BarData({required this.label, required this.value, required this.color});

  final String label;
  final double value;
  final Color color;
}

/// Displays a simple vertical bar chart.
class BarChart extends StatelessWidget {
  const BarChart({super.key, required this.bars, this.height = 240});

  final List<BarData> bars;
  final double height;

  @override
  Widget build(BuildContext context) {
    return Semantics(
      label: 'Bar chart with ${bars.length} bars',
      child: CustomPaint(
        size: Size(double.infinity, height),
        painter: _BarChartPainter(bars: bars),
      ),
    );
  }
}

class _BarChartPainter extends CustomPainter {
  _BarChartPainter({required this.bars});

  final List<BarData> bars;

  static const double _labelHeight = 24;
  static const double _topPadding = 16;
  static const double _barSpacing = 12;

  @override
  void paint(Canvas canvas, Size size) {
    if (bars.isEmpty) return;

    final maxValue = bars.map((b) => b.value).reduce(math.max);
    if (maxValue == 0) return;

    final chartHeight = size.height - _labelHeight - _topPadding;
    final barWidth =
        (size.width - _barSpacing * (bars.length + 1)) / bars.length;

    for (var i = 0; i < bars.length; i++) {
      final bar = bars[i];
      final x = _barSpacing + i * (barWidth + _barSpacing);
      final normalised = bar.value / maxValue;
      final barHeight = chartHeight * normalised;
      final y = _topPadding + chartHeight - barHeight;

      // Draw the bar.
      final rrect = RRect.fromRectAndCorners(
        Rect.fromLTWH(x, y, barWidth, barHeight),
        topLeft: const Radius.circular(4),
        topRight: const Radius.circular(4),
      );
      canvas.drawRRect(rrect, Paint()..color = bar.color);

      // Draw the value above the bar.
      final valuePainter = TextPainter(
        text: TextSpan(
          text: bar.value.toStringAsFixed(0),
          style: TextStyle(
            color: bar.color,
            fontSize: 11,
            fontWeight: FontWeight.w600,
          ),
        ),
        textDirection: TextDirection.ltr,
      )..layout();
      valuePainter.paint(
        canvas,
        Offset(x + (barWidth - valuePainter.width) / 2, y - 16),
      );

      // Draw the label below the bar.
      final labelPainter = TextPainter(
        text: TextSpan(
          text: bar.label,
          style: const TextStyle(color: Color(0xFF757575), fontSize: 11),
        ),
        textDirection: TextDirection.ltr,
        maxLines: 1,
        ellipsis: '.',
      )..layout(maxWidth: barWidth);
      labelPainter.paint(
        canvas,
        Offset(
          x + (barWidth - labelPainter.width) / 2,
          size.height - _labelHeight + 4,
        ),
      );
    }
  }

  @override
  bool shouldRepaint(covariant _BarChartPainter oldDelegate) {
    return oldDelegate.bars != bars;
  }
}
```

### Line Chart

A complete, self-contained line chart widget with gradient fill.

```dart
import 'dart:math' as math;
import 'dart:ui' as ui;
import 'package:flutter/material.dart';

/// A data point on the line chart.
class LineDataPoint {
  const LineDataPoint({required this.x, required this.y});

  final double x;
  final double y;
}

class LineChart extends StatelessWidget {
  const LineChart({
    super.key,
    required this.points,
    this.lineColor = const Color(0xFF2196F3),
    this.height = 200,
  });

  final List<LineDataPoint> points;
  final Color lineColor;
  final double height;

  @override
  Widget build(BuildContext context) {
    return Semantics(
      label: 'Line chart with ${points.length} data points',
      child: CustomPaint(
        size: Size(double.infinity, height),
        painter: _LineChartPainter(points: points, lineColor: lineColor),
      ),
    );
  }
}

class _LineChartPainter extends CustomPainter {
  _LineChartPainter({required this.points, required this.lineColor});

  final List<LineDataPoint> points;
  final Color lineColor;

  static const double _padding = 24;

  @override
  void paint(Canvas canvas, Size size) {
    if (points.length < 2) return;

    final drawWidth = size.width - _padding * 2;
    final drawHeight = size.height - _padding * 2;

    final minX = points.map((p) => p.x).reduce(math.min);
    final maxX = points.map((p) => p.x).reduce(math.max);
    final minY = points.map((p) => p.y).reduce(math.min);
    final maxY = points.map((p) => p.y).reduce(math.max);
    final rangeX = maxX - minX;
    final rangeY = maxY - minY;

    Offset toCanvas(LineDataPoint p) {
      final nx = rangeX == 0 ? 0.5 : (p.x - minX) / rangeX;
      final ny = rangeY == 0 ? 0.5 : (p.y - minY) / rangeY;
      return Offset(
        _padding + nx * drawWidth,
        _padding + (1 - ny) * drawHeight,
      );
    }

    // Build the line path.
    final linePath = Path();
    final first = toCanvas(points.first);
    linePath.moveTo(first.dx, first.dy);
    for (var i = 1; i < points.length; i++) {
      final p = toCanvas(points[i]);
      linePath.lineTo(p.dx, p.dy);
    }

    // Fill gradient below the line.
    final fillPath = Path.from(linePath)
      ..lineTo(_padding + drawWidth, _padding + drawHeight)
      ..lineTo(_padding, _padding + drawHeight)
      ..close();

    final gradientPaint = Paint()
      ..shader = ui.Gradient.linear(
        Offset(0, _padding),
        Offset(0, _padding + drawHeight),
        [lineColor.withAlpha(80), lineColor.withAlpha(0)],
      );
    canvas.drawPath(fillPath, gradientPaint);

    // Draw the line.
    final linePaint = Paint()
      ..color = lineColor
      ..style = PaintingStyle.stroke
      ..strokeWidth = 2.5
      ..strokeCap = StrokeCap.round
      ..strokeJoin = StrokeJoin.round;
    canvas.drawPath(linePath, linePaint);

    // Draw dots at each point.
    final dotPaint = Paint()..color = lineColor;
    for (final point in points) {
      canvas.drawCircle(toCanvas(point), 4, dotPaint);
    }
  }

  @override
  bool shouldRepaint(covariant _LineChartPainter oldDelegate) {
    return oldDelegate.points != points || oldDelegate.lineColor != lineColor;
  }
}
```

---

## Performance Optimization

### `shouldRepaint`

Always compare the fields that affect visual output.

```dart
@override
bool shouldRepaint(covariant MyPainter oldDelegate) {
  return oldDelegate.progress != progress ||
      oldDelegate.color != color ||
      oldDelegate.data != data;
}
```

Avoid returning `true` unconditionally -- this causes a repaint every frame even when nothing has changed.

### RepaintBoundary

Wrap expensive painters in `RepaintBoundary` to isolate their compositing layer.

```dart
Widget build(BuildContext context) {
  return RepaintBoundary(
    child: CustomPaint(
      painter: ExpensiveChartPainter(data: data),
      size: const Size(400, 300),
    ),
  );
}
```

### Cache Complex Paths

If a path is expensive to compute and only depends on the size, cache it.

```dart
class CachedPathPainter extends CustomPainter {
  CachedPathPainter({required this.progress});

  final double progress;

  Size? _cachedSize;
  Path? _cachedPath;

  @override
  void paint(Canvas canvas, Size size) {
    if (_cachedPath == null || _cachedSize != size) {
      _cachedSize = size;
      _cachedPath = _buildExpensivePath(size);
    }

    final metrics = _cachedPath!.computeMetrics().first;
    final visible = metrics.extractPath(0, metrics.length * progress);
    canvas.drawPath(visible, Paint()
      ..color = const Color(0xFF2196F3)
      ..style = PaintingStyle.stroke
      ..strokeWidth = 3);
  }

  Path _buildExpensivePath(Size size) {
    // ... complex path computation
    return Path()
      ..moveTo(0, size.height / 2)
      ..lineTo(size.width, size.height / 2);
  }

  @override
  bool shouldRepaint(covariant CachedPathPainter oldDelegate) {
    return oldDelegate.progress != progress;
  }
}
```

### Minimize `canvas.save()` / `canvas.restore()`

Each save/restore pair adds overhead. Batch drawing operations that share the same clip or transform.

---

## Touch Interaction

Combine `GestureDetector` with a custom painter to handle taps and drags on painted elements.

```dart
import 'dart:math' as math;
import 'package:flutter/material.dart';

class InteractiveDotCanvas extends StatefulWidget {
  const InteractiveDotCanvas({super.key});

  @override
  State<InteractiveDotCanvas> createState() => _InteractiveDotCanvasState();
}

class _InteractiveDotCanvasState extends State<InteractiveDotCanvas> {
  final List<Offset> _dots = [];
  int? _selectedIndex;

  @override
  Widget build(BuildContext context) {
    return GestureDetector(
      onTapDown: (details) {
        final box = context.findRenderObject()! as RenderBox;
        final local = box.globalToLocal(details.globalPosition);

        // Check if tapping an existing dot.
        final hitIndex = _hitTest(local);
        if (hitIndex != null) {
          setState(() => _selectedIndex = hitIndex);
        } else {
          setState(() {
            _dots.add(local);
            _selectedIndex = _dots.length - 1;
          });
        }
      },
      onPanUpdate: (details) {
        if (_selectedIndex != null) {
          final box = context.findRenderObject()! as RenderBox;
          final local = box.globalToLocal(details.globalPosition);
          setState(() => _dots[_selectedIndex!] = local);
        }
      },
      onPanEnd: (_) => setState(() => _selectedIndex = null),
      child: CustomPaint(
        size: Size.infinite,
        painter: _DotPainter(dots: _dots, selectedIndex: _selectedIndex),
      ),
    );
  }

  int? _hitTest(Offset position) {
    const hitRadius = 20.0;
    for (var i = _dots.length - 1; i >= 0; i--) {
      if ((_dots[i] - position).distance < hitRadius) return i;
    }
    return null;
  }
}

class _DotPainter extends CustomPainter {
  _DotPainter({required this.dots, this.selectedIndex});

  final List<Offset> dots;
  final int? selectedIndex;

  @override
  void paint(Canvas canvas, Size size) {
    for (var i = 0; i < dots.length; i++) {
      final isSelected = i == selectedIndex;
      final paint = Paint()
        ..color = isSelected
            ? const Color(0xFFE91E63)
            : const Color(0xFF2196F3)
        ..style = PaintingStyle.fill;

      canvas.drawCircle(dots[i], isSelected ? 14 : 10, paint);
    }
  }

  @override
  bool shouldRepaint(covariant _DotPainter oldDelegate) {
    return oldDelegate.dots != dots ||
        oldDelegate.selectedIndex != selectedIndex;
  }
}
```

---

## Animating Custom Painters

### Using AnimationController

Drive a custom painter with a `Listenable` (typically an `AnimationController`).

```dart
import 'dart:math' as math;
import 'package:flutter/material.dart';

class PulsingRingWidget extends StatefulWidget {
  const PulsingRingWidget({super.key, required this.color});

  final Color color;

  @override
  State<PulsingRingWidget> createState() => _PulsingRingWidgetState();
}

class _PulsingRingWidgetState extends State<PulsingRingWidget>
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
      child: CustomPaint(
        painter: _PulsingRingPainter(
          animation: _controller,
          color: widget.color,
        ),
        size: const Size(200, 200),
      ),
    );
  }
}

class _PulsingRingPainter extends CustomPainter {
  _PulsingRingPainter({
    required this.animation,
    required this.color,
  }) : super(repaint: animation);

  final Animation<double> animation;
  final Color color;

  @override
  void paint(Canvas canvas, Size size) {
    final center = Offset(size.width / 2, size.height / 2);
    final maxRadius = size.shortestSide / 2;

    for (var i = 0; i < 3; i++) {
      final t = (animation.value + i / 3) % 1.0;
      final radius = maxRadius * t;
      final alpha = ((1.0 - t) * 200).round().clamp(0, 255);

      canvas.drawCircle(
        center,
        radius,
        Paint()
          ..color = color.withAlpha(alpha)
          ..style = PaintingStyle.stroke
          ..strokeWidth = 3,
      );
    }
  }

  @override
  bool shouldRepaint(covariant _PulsingRingPainter oldDelegate) {
    return oldDelegate.color != color;
  }
}
```

Key points:

- Pass the `AnimationController` as the `repaint` listenable in the `super` constructor. This tells the framework to repaint whenever the animation ticks, without needing `setState`.
- Wrap in `RepaintBoundary` to keep the animated layer isolated.
- `shouldRepaint` only needs to compare non-animated properties (like `color`) because the `repaint` listenable already handles animation-driven repaints.

### Path Drawing Animation

Animate a path being drawn over time.

```dart
import 'package:flutter/material.dart';

class AnimatedPathWidget extends StatefulWidget {
  const AnimatedPathWidget({super.key});

  @override
  State<AnimatedPathWidget> createState() => _AnimatedPathWidgetState();
}

class _AnimatedPathWidgetState extends State<AnimatedPathWidget>
    with SingleTickerProviderStateMixin {
  late final AnimationController _controller;

  @override
  void initState() {
    super.initState();
    _controller = AnimationController(
      vsync: this,
      duration: const Duration(seconds: 3),
    )..forward();
  }

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return RepaintBoundary(
      child: CustomPaint(
        painter: _PathRevealPainter(animation: _controller),
        size: const Size(300, 300),
      ),
    );
  }
}

class _PathRevealPainter extends CustomPainter {
  _PathRevealPainter({required this.animation}) : super(repaint: animation);

  final Animation<double> animation;

  @override
  void paint(Canvas canvas, Size size) {
    final fullPath = Path()
      ..moveTo(size.width * 0.1, size.height * 0.9)
      ..cubicTo(
        size.width * 0.25, size.height * 0.1,
        size.width * 0.75, size.height * 0.1,
        size.width * 0.9, size.height * 0.9,
      );

    final metric = fullPath.computeMetrics().first;
    final visiblePath = metric.extractPath(0, metric.length * animation.value);

    canvas.drawPath(
      visiblePath,
      Paint()
        ..color = const Color(0xFF6200EA)
        ..style = PaintingStyle.stroke
        ..strokeWidth = 4
        ..strokeCap = StrokeCap.round,
    );
  }

  @override
  bool shouldRepaint(covariant _PathRevealPainter oldDelegate) => false;
}
```
