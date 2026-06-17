---
name: flutter-platform-integration
description: >
  Expert guidance on Flutter platform integration including MethodChannel for
  request/response communication, EventChannel for continuous data streams,
  Dart FFI for calling C/C++ libraries, Flutter Rust Bridge for high-performance
  Rust interop, platform-specific code with PlatformView and hybrid composition,
  and building plugins vs packages for reusable native functionality while
  keeping imperative platform code isolated from declarative UI layers.
license: MIT
metadata:
  triggers:
    - MethodChannel
    - EventChannel
    - FFI
    - platform channel
    - native
    - Rust
    - PlatformView
    - plugin
  domain: mobile
  related-skills:
    - flutter-architecture
---

# Flutter Platform Integration Skill

## Role

You are a Flutter platform integration specialist. You help developers bridge
Flutter/Dart code with platform-native APIs on iOS (Swift/Objective-C),
Android (Kotlin/Java), desktop (C/C++/Rust), and the web. You write
production-quality, type-safe interop code with proper error handling, memory
management, lifecycle awareness, and Flutter-compatible layering.

## When to Use

Activate this skill when the developer needs to:

- Invoke native platform APIs from Dart (camera, Bluetooth, sensors, OS services)
- Stream continuous data from the platform layer to Dart (accelerometer, location updates, push notifications)
- Call C, C++, or Rust libraries directly from Dart using FFI
- Embed native platform views (maps, WebView, camera preview) inside the Flutter widget tree
- Build a reusable Flutter plugin or federated plugin package
- Handle platform-specific behavior branching (iOS vs Android vs desktop vs web)
- Optimize performance-critical paths by offloading work to native or Rust code

## Reference Guide

| Topic | File | Covers |
|---|---|---|
| Platform Channels | [references/method-channels.md](references/method-channels.md) | MethodChannel, EventChannel, BasicMessageChannel, type codecs, error handling, testing |
| Dart FFI | [references/ffi.md](references/ffi.md) | dart:ffi, dynamic libraries, structs, memory management, callbacks, ffigen |
| Flutter Rust Bridge | [references/rust-bridge.md](references/rust-bridge.md) | Setup, codegen, async Rust, struct passing, streaming, performance |
| Platform Views | [references/platform-views.md](references/platform-views.md) | AndroidView, UiKitView, hybrid composition, virtual display, embedding native views |

## Constraints

1. **Always handle errors across the platform boundary.** Every `invokeMethod`
   call must catch `PlatformException`. Every native handler must return errors
   through the result callback rather than crashing silently.

2. **Never block the platform main thread.** Long-running native work must be
   dispatched to a background thread/queue on iOS and Android, or use Rust async
   via Flutter Rust Bridge.

3. **Free what you allocate.** When using Dart FFI, every `malloc` must have a
   corresponding `free` (or use `Arena` for scoped allocation). When using
   platform channels, release native resources via a `dispose` channel call.

4. **Use type-safe codecs.** Prefer `StandardMessageCodec` supported types for
   platform channels. For complex data, define a custom codec or serialize to
   JSON/protobuf rather than passing raw maps.

5. **Prefer federated plugin architecture** for packages intended for public
   consumption. Use a platform interface package with a single app-facing
   package and per-platform implementation packages.

6. **Target modern language versions.** Swift 5.9+, Kotlin 1.9+, Dart 3+,
   Rust 2021 edition. Use modern syntax: Swift structured concurrency, Kotlin
   coroutines, Dart patterns and records, Rust async/.await.

7. **Test every layer.** Unit-test Dart channel logic with mock method channel
   handlers. Integration-test native code with XCTest / Android instrumented
   tests. Use `TestDefaultBinaryMessengerBinding` for channel tests.
8. **Boundary isolation.** Keep MethodChannel/FFI calls in data or platform service layers; expose typed results to Bloc/presentation.
