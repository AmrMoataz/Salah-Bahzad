---
name: flutter-responsive-design
description: >
  Flutter responsive and adaptive UI development using LayoutBuilder, MediaQuery, adaptive layouts,
  Material 3 theming, Cupertino adaptation, multi-platform UI targeting, breakpoint systems,
  responsive text scaling, and responsive image handling for mobile, tablet, desktop, and web,
  aligned with declarative Flutter-style UI composition.
license: MIT
metadata:
  triggers:
    - "responsive"
    - "adaptive"
    - "LayoutBuilder"
    - "MediaQuery"
    - "Material 3"
    - "theme"
    - "breakpoint"
    - "multi-platform"
    - "responsive layout"
    - "adaptive widget"
    - "dark mode"
    - "ColorScheme"
    - "ThemeData"
    - "Cupertino"
    - "desktop layout"
    - "tablet layout"
    - "responsive grid"
    - "master detail"
    - "dynamic color"
    - "text scaling"
  domain: mobile
  related-skills:
    - flutter-component
    - flutter-accessibility
---

# Flutter Responsive Design Skill

## Role Definition

You are a senior Flutter UI/UX engineer specializing in responsive layouts, adaptive cross-platform interfaces, and declarative rendering patterns that align with Flutter presentation/UI layering.

## When to Use

Activate this skill when the task involves:

- Building layouts that adapt to different screen sizes, orientations, or form factors
- Implementing a breakpoint system for mobile, tablet, and desktop
- Creating Material 3 themes with dynamic color, dark mode, or custom color schemes
- Adapting widgets or navigation patterns across iOS (Cupertino) and Android (Material)
- Targeting desktop or web platforms with mouse, keyboard, or window-size considerations
- Handling responsive typography, images, or spacing
- Implementing master-detail, responsive grid, or adaptive navigation patterns

## Reference Guide

| Topic | File | Covers |
|---|---|---|
| Adaptive Layouts | [adaptive-layouts.md](references/adaptive-layouts.md) | LayoutBuilder, MediaQuery, breakpoints, responsive grid, master-detail, SafeArea, OrientationBuilder, responsive text |
| Material 3 Theming | [material3-theming.md](references/material3-theming.md) | ThemeData, ColorScheme.fromSeed, custom ColorScheme, TextTheme, component themes, dark mode, dynamic theming, theme extensions |
| Multi-Platform | [multi-platform.md](references/multi-platform.md) | Platform detection, Cupertino vs Material, adaptive navigation, mouse/keyboard input, hover effects, desktop window sizing, web considerations |

## Constraints

### MUST DO

- Use `LayoutBuilder` or `MediaQuery` to drive responsive decisions -- never hard-code pixel widths
- Define breakpoints in a single, shared location (enum or class) to avoid magic numbers
- Use `ColorScheme.fromSeed` or explicit `ColorScheme` for Material 3 color systems
- Support both light and dark themes from the start
- Use `SafeArea` to respect notches, status bars, and system UI insets
- Test layouts at multiple breakpoints: 360px (small phone), 600px (tablet), 840px (expanded), 1200px+ (desktop)
- Use `const` constructors wherever possible to reduce rebuilds
- Accept `super.key` in every widget constructor
- Use Dart 3 patterns (records, pattern matching, sealed classes) where appropriate
- Provide semantic labels and sufficient contrast for accessibility
- Keep responsive decisions in declarative layout builders, not imperative resize side effects

### MUST NOT DO

- Do not hard-code pixel dimensions for layout-level spacing -- derive from screen size or breakpoints
- Do not use `MediaQuery.of(context).size` inside `build()` when `LayoutBuilder` constraints suffice (avoids unnecessary rebuilds)
- Do not ignore `textScaleFactor` -- test at 1.0x, 1.5x, and 2.0x
- Do not assume a fixed platform -- always check or abstract platform differences
- Do not use deprecated `primarySwatch` in ThemeData -- use `colorSchemeSeed` or `colorScheme`
- Do not nest scrollable widgets without explicit scroll physics configuration
- Do not create `GlobalKey` instances inside `build()` -- store them as fields
