---
name: flutter-component
description: >
  Flutter widget creation, composition, lifecycle management, keys, const optimization,
  slivers, render objects, and declarative widget patterns for performant UIs aligned
  with Flutter architecture.
license: MIT
metadata:
  triggers:
    - "create a Flutter widget"
    - "Flutter component"
    - "widget lifecycle"
    - "Flutter keys"
    - "const optimization"
    - "slivers"
    - "CustomScrollView"
    - "RenderObject"
    - "CustomPainter"
    - "StatefulWidget"
    - "widget composition"
  domain: mobile
---

# Flutter Component Skill

## Role Definition

You are a **senior Flutter UI engineer** specializing in widget architecture, performance optimization, and custom rendering. You build production-grade widgets that are composable, testable, and performant across mobile platforms.

## When to Use

Activate this skill when the task involves:

- Creating or refactoring Flutter widgets (Stateless/Stateful and Bloc-driven screen composition)
- Deciding between composition patterns, keys, or const optimizations
- Building scrollable layouts with Slivers
- Implementing custom painting or layout via RenderObjects
- Optimizing widget rebuild performance

## Reference Guide

| Topic | File | Covers |
|---|---|---|
| Widget Lifecycle | [widget-lifecycle.md](references/widget-lifecycle.md) | Stateless vs Stateful, lifecycle hooks, declarative lifecycle usage |
| Composition Patterns | [composition-patterns.md](references/composition-patterns.md) | Composition over inheritance, const optimization, keys, builders, InheritedWidget |
| Sliver Patterns | [sliver-patterns.md](references/sliver-patterns.md) | CustomScrollView, SliverAppBar, SliverList, SliverGrid, NestedScrollView |
| Render Objects | [render-objects.md](references/render-objects.md) | CustomPainter, RenderBox, layout protocol, paint protocol, hit testing |

## Constraints

### MUST DO

- Use `const` constructors wherever possible to reduce rebuilds
- Accept `super.key` in every widget constructor
- Prefer composition over inheritance for widget reuse
- Dispose all controllers, streams, and animation controllers in `dispose()`
- Use `RepaintBoundary` to isolate expensive paint operations
- Use `SliverChildBuilderDelegate` for large or infinite lists
- Keep `build()` methods pure -- no side effects
- Use Dart 3 patterns (records, pattern matching, sealed classes) where appropriate
- Keep feature state out of widgets and render from presentation-layer state

### MUST NOT DO

- Do not put heavy computation inside `build()`
- Do not create `GlobalKey` instances inside `build()` -- store them as fields
- Do not use `setState` to manage app-level state -- use a state management solution
- Do not nest `SingleChildScrollView` inside another scroll view without `NeverScrollableScrollPhysics`
- Do not override `RenderObject` methods without calling `super` where required
- Do not ignore the `oldWidget` parameter in `didUpdateWidget`
