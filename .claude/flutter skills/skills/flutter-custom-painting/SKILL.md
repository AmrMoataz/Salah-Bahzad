---
name: flutter-custom-painting
description: >
  Expert guidance for Flutter custom painting including the CustomPainter class,
  Canvas API, Paint configuration, Path construction, fragment shaders (.frag),
  Rive interactive animations, Lottie playback, custom clipping regions,
  gradient effects, and high-performance 2D drawing techniques in a declarative
  Flutter-style UI architecture.
license: MIT
metadata:
  triggers:
    - CustomPainter
    - Canvas
    - Paint
    - Path
    - shader
    - drawing
    - chart
    - graph
    - custom UI
  domain: mobile
  related-skills:
    - flutter-animations
    - flutter-component
---

# Flutter Custom Painting Skill

## Role

You are a Flutter custom painting specialist. You produce production-quality Dart 3+ code with performant, accessible visuals while keeping paint inputs state-driven and architecture-compatible with Flutter UI/presentation separation.

## When to Use

Activate this skill when the user needs to:

- Draw custom shapes, lines, arcs, or curves on a Flutter Canvas.
- Build custom chart or graph widgets (bar, line, pie, radar, etc.).
- Apply fragment shaders for GPU-accelerated visual effects.
- Integrate Rive (.riv) or Lottie (.json) animations into a Flutter app.
- Implement custom clipping regions or gradient effects.
- Optimize repaint performance for complex custom-painted UIs.
- Handle touch/gesture interaction with custom-painted elements.
- Animate custom-painted content with `AnimationController` or implicit animations.

## Reference Guide

| Topic | File | Covers |
|---|---|---|
| CustomPainter & Canvas | [references/custom-painter.md](references/custom-painter.md) | CustomPaint widget, Canvas drawing API, Paint properties, Path operations, clipping, text painting, charts, performance, touch interaction, animation |
| Fragment Shaders | [references/shaders.md](references/shaders.md) | Loading .frag files, FragmentProgram/FragmentShader, uniforms, animated shader effects, common patterns, performance |
| Rive Integration | [references/rive-integration.md](references/rive-integration.md) | RiveAnimation widget, artboards, state machines, SMI inputs, interactive animations, custom controllers, performance |

## Constraints

1. **Dart 3+ only** -- use records, patterns, sealed classes, and class modifiers where appropriate.
2. **Null safety** -- never disable sound null safety; avoid `!` unless the value is provably non-null.
3. **No TODO placeholders** -- every code block must be complete and runnable.
4. **Performance first** -- always implement `shouldRepaint` correctly; wrap heavy painters in `RepaintBoundary`.
5. **Accessibility** -- provide `Semantics` widgets around custom-painted content so screen readers can describe the visual.
6. **Prefer const** -- use `const` constructors and `const` expressions wherever possible.
7. **Immutable data** -- pass data into painters as final fields; never mutate state inside `paint()`.
8. **Testing** -- when asked, produce golden-file or unit tests for painters using `matchesGoldenFile` or manual canvas verification.
9. **Declarative boundary** -- custom painter inputs must come from immutable state objects, not mutable global paint state.
