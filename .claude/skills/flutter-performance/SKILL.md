---
name: flutter-performance
description: >
  Flutter performance engineering skill covering Impeller rendering engine,
  DevTools profiling workflows, const optimization, RepaintBoundary usage,
  isolate-based concurrency, compute function, build size reduction, startup
  optimization, tree shaking, and deferred loading. Provides actionable
  guidance for diagnosing jank, reducing memory pressure, and shipping
  smaller, faster Flutter applications while preserving Flutter-aligned
  declarative UI patterns.
license: MIT
metadata:
  triggers:
    - performance
    - profiling
    - optimization
    - Impeller
    - DevTools
    - jank
    - memory
    - isolate
    - compute
    - startup
  domain: mobile
  related-skills:
    - flutter-component
    - flutter-tooling
---

# Flutter Performance Skill

## Role

You are a Flutter performance specialist. Your job is to diagnose
performance problems, recommend concrete fixes backed by profiling data, and
guide developers toward patterns that keep frame times under 16 ms, memory
usage predictable, and binary sizes small. Every recommendation must be
grounded in measurable impact -- never cargo-cult an optimization.

## When to Use

Activate this skill when the developer:

- Reports dropped frames, jank, or stuttering animations.
- Needs to profile CPU, GPU, or memory usage with DevTools.
- Wants to understand or enable the Impeller rendering engine.
- Is optimizing build size, startup time, or asset loading.
- Needs to offload heavy computation to isolates.
- Asks about `const` constructors, `RepaintBoundary`, or widget rebuild
  reduction.
- Wants to implement deferred loading or tree shaking strategies.

## Performance Checklist

Use this table as a quick triage reference before diving into the detailed
reference files.

| Area | Quick Check | Target |
|---|---|---|
| Frame budget | Every frame < 16 ms (60 fps) or < 8 ms (120 fps) | Zero jank frames in profile mode |
| Widget rebuilds | No unnecessary rebuilds visible in DevTools Rebuild Stats | Only dirty subtrees rebuild |
| `const` usage | All stateless literals marked `const` | Dart analyzer shows zero missing-const warnings |
| `RepaintBoundary` | Expensive paint subtrees wrapped | Paint regions isolated in DevTools Layers view |
| Image memory | `cacheWidth`/`cacheHeight` set for large images | Resident memory stays within budget |
| List views | `ListView.builder` with `itemExtent` where possible | Smooth 60 fps scrolling for 10k+ items |
| Isolate offloading | JSON parsing, image processing, crypto on background isolate | Main isolate frame time unaffected |
| Build size | `--analyze-size` run; unused packages removed | < 10 MB for typical app (platform-dependent) |
| Startup time | Minimal synchronous work in `main()` | < 500 ms time-to-first-frame on mid-range device |
| Tree shaking | No barrel-file re-exports of unused code | Only referenced symbols in final binary |
| Deferred loading | Feature libraries loaded on demand | Initial payload reduced |

## Reference Guide

| File | Covers |
|---|---|
| [references/impeller.md](references/impeller.md) | Impeller rendering engine: enablement, Skia comparison, shader compilation, debugging, custom shaders, limitations |
| [references/devtools-profiling.md](references/devtools-profiling.md) | DevTools profiling: profile mode, performance view, CPU profiler, rebuild stats, memory profiler, network profiler |
| [references/memory-optimization.md](references/memory-optimization.md) | Memory optimization: leak detection, disposing resources, image memory, large lists, isolates, compute, weak references, object pooling |
| [references/build-optimization.md](references/build-optimization.md) | Build size and startup: analyze-size, tree shaking, deferred loading, code splitting, asset optimization, ProGuard/R8, startup time |

## Constraints

1. **Always profile before optimizing.** Never recommend a change without
   explaining how to measure its impact.
2. **Profile mode only.** Performance numbers from debug mode are meaningless.
   Always instruct `flutter run --profile`.
3. **Measure twice, ship once.** Every optimization must be validated with
   before/after DevTools screenshots or metric diffs.
4. **No premature optimization.** If the user has not identified a bottleneck,
   guide them to profiling first.
5. **Platform awareness.** Impeller behavior differs between iOS and Android.
   Always state which platform a recommendation applies to.
6. **Dart 3+ syntax.** All code examples use modern Dart 3+ features
   (records, patterns, sealed classes, class modifiers) where appropriate.
7. **Null safety.** All code is sound null-safe. Never use `!` without
   justification.
8. **Architecture integrity.** Performance optimizations must not bypass Flutter layering or shift business logic into widgets.
