# Dart FFI Guide

Dart Foreign Function Interface (`dart:ffi`) allows Dart code to call native
C APIs directly, without going through platform channels. This is ideal for
CPU-intensive work, existing C/C++ libraries, and low-latency interop.

---

## 1. dart:ffi Basics

```dart
import 'dart:ffi' as ffi;

// Declare a native function signature.
// C: int add(int a, int b);
typedef AddNative = ffi.Int32 Function(ffi.Int32 a, ffi.Int32 b);

// Dart function signature that mirrors the native one.
typedef AddDart = int Function(int a, int b);
```

Key FFI types:

| C Type | dart:ffi Type | Dart Type |
|---|---|---|
| `int` / `int32_t` | `Int32` | `int` |
| `long` / `int64_t` | `Int64` | `int` |
| `float` | `Float` | `double` |
| `double` | `Double` | `double` |
| `void` | `Void` | `void` |
| `char*` | `Pointer<Utf8>` | use `package:ffi` |
| `bool` | `Bool` | `bool` |
| `uint8_t` | `Uint8` | `int` |
| `size_t` | `Size` | `int` |

---

## 2. Loading Dynamic Libraries

```dart
import 'dart:ffi' as ffi;
import 'dart:io' show Platform;

/// Loads the native library for the current platform.
ffi.DynamicLibrary loadNativeLibrary() {
  if (Platform.isAndroid) {
    // Android loads shared objects from the jniLibs directory.
    return ffi.DynamicLibrary.open('libimage_processing.so');
  }
  if (Platform.isIOS) {
    // iOS statically links; use the process itself.
    return ffi.DynamicLibrary.process();
  }
  if (Platform.isMacOS) {
    return ffi.DynamicLibrary.open('libimage_processing.dylib');
  }
  if (Platform.isWindows) {
    return ffi.DynamicLibrary.open('image_processing.dll');
  }
  if (Platform.isLinux) {
    return ffi.DynamicLibrary.open('libimage_processing.so');
  }
  throw UnsupportedError('Unsupported platform: ${Platform.operatingSystem}');
}
```

---

## 3. Calling C Functions from Dart

Given the following C header:

```c
// image_processing.h
#include <stdint.h>

int32_t apply_grayscale(uint8_t* pixels, int32_t length);
double  compute_brightness(const uint8_t* pixels, int32_t length);
void    free_buffer(uint8_t* buffer);
uint8_t* allocate_buffer(int32_t size);
```

Dart bindings:

```dart
import 'dart:ffi' as ffi;
import 'dart:typed_data';
import 'package:ffi/ffi.dart';

// Native type signatures.
typedef ApplyGrayscaleNative = ffi.Int32 Function(
  ffi.Pointer<ffi.Uint8> pixels,
  ffi.Int32 length,
);
typedef ApplyGrayscaleDart = int Function(
  ffi.Pointer<ffi.Uint8> pixels,
  int length,
);

typedef ComputeBrightnessNative = ffi.Double Function(
  ffi.Pointer<ffi.Uint8> pixels,
  ffi.Int32 length,
);
typedef ComputeBrightnessDart = double Function(
  ffi.Pointer<ffi.Uint8> pixels,
  int length,
);

typedef AllocateBufferNative = ffi.Pointer<ffi.Uint8> Function(ffi.Int32 size);
typedef AllocateBufferDart = ffi.Pointer<ffi.Uint8> Function(int size);

typedef FreeBufferNative = ffi.Void Function(ffi.Pointer<ffi.Uint8> buffer);
typedef FreeBufferDart = void Function(ffi.Pointer<ffi.Uint8> buffer);

class ImageProcessing {
  ImageProcessing() : _lib = loadNativeLibrary();

  final ffi.DynamicLibrary _lib;

  late final ApplyGrayscaleDart _applyGrayscale = _lib
      .lookupFunction<ApplyGrayscaleNative, ApplyGrayscaleDart>(
        'apply_grayscale',
      );

  late final ComputeBrightnessDart _computeBrightness = _lib
      .lookupFunction<ComputeBrightnessNative, ComputeBrightnessDart>(
        'compute_brightness',
      );

  late final AllocateBufferDart _allocateBuffer = _lib
      .lookupFunction<AllocateBufferNative, AllocateBufferDart>(
        'allocate_buffer',
      );

  late final FreeBufferDart _freeBuffer = _lib
      .lookupFunction<FreeBufferNative, FreeBufferDart>('free_buffer');

  /// Converts [imageBytes] to grayscale in-place using native code.
  /// Returns the number of pixels processed.
  int applyGrayscale(Uint8List imageBytes) {
    final ptr = _allocateBuffer(imageBytes.length);
    try {
      // Copy Dart bytes into native memory.
      ptr.asTypedList(imageBytes.length).setAll(0, imageBytes);

      final processed = _applyGrayscale(ptr, imageBytes.length);

      // Copy results back to Dart.
      imageBytes.setAll(0, ptr.asTypedList(imageBytes.length));

      return processed;
    } finally {
      _freeBuffer(ptr);
    }
  }

  /// Computes average brightness of the pixel buffer.
  double computeBrightness(Uint8List imageBytes) {
    final ptr = _allocateBuffer(imageBytes.length);
    try {
      ptr.asTypedList(imageBytes.length).setAll(0, imageBytes);
      return _computeBrightness(ptr, imageBytes.length);
    } finally {
      _freeBuffer(ptr);
    }
  }
}
```

---

## 4. Struct Definitions

Map C structs to Dart using `ffi.Struct`:

```c
// C struct
typedef struct {
    double x;
    double y;
    double z;
    int64_t timestamp_us;
} SensorReading;

SensorReading get_latest_reading(void);
void process_readings(SensorReading* readings, int32_t count);
```

```dart
import 'dart:ffi' as ffi;

final class SensorReading extends ffi.Struct {
  @ffi.Double()
  external double x;

  @ffi.Double()
  external double y;

  @ffi.Double()
  external double z;

  @ffi.Int64()
  external int timestampUs;

  @override
  String toString() =>
      'SensorReading(x: $x, y: $y, z: $z, t: $timestampUs)';
}

// Native signatures.
typedef GetLatestReadingNative = SensorReading Function();
typedef GetLatestReadingDart = SensorReading Function();

typedef ProcessReadingsNative = ffi.Void Function(
  ffi.Pointer<SensorReading> readings,
  ffi.Int32 count,
);
typedef ProcessReadingsDart = void Function(
  ffi.Pointer<SensorReading> readings,
  int count,
);
```

### Nested and Array Structs

```c
typedef struct {
    char name[64];
    SensorReading readings[10];
    int32_t reading_count;
} SensorDevice;
```

```dart
import 'dart:ffi' as ffi;

final class SensorDevice extends ffi.Struct {
  @ffi.Array(64)
  external ffi.Array<ffi.Uint8> name;

  @ffi.Array(10)
  external ffi.Array<SensorReading> readings;

  @ffi.Int32()
  external int readingCount;
}
```

---

## 5. Memory Management

### 5.1 Manual malloc/free

```dart
import 'dart:ffi' as ffi;
import 'package:ffi/ffi.dart';

void manualAllocation() {
  // Allocate a single int.
  final intPtr = calloc<ffi.Int32>();
  intPtr.value = 42;
  print('Value: ${intPtr.value}');
  calloc.free(intPtr);

  // Allocate an array of 100 doubles.
  final arrayPtr = calloc<ffi.Double>(100);
  for (var i = 0; i < 100; i++) {
    arrayPtr[i] = i * 1.5;
  }
  calloc.free(arrayPtr);
}
```

### 5.2 Scoped Allocation with Arena

`Arena` automatically frees all allocations when the scope exits, preventing
memory leaks:

```dart
import 'dart:ffi' as ffi;
import 'package:ffi/ffi.dart';

void scopedAllocation() {
  using((Arena arena) {
    // All allocations in this block are freed when the block exits.
    final buffer = arena<ffi.Uint8>(1024);
    final name = 'Hello FFI'.toNativeUtf8(allocator: arena);
    final count = arena<ffi.Int32>();

    count.value = 1024;

    // Use buffer, name, count...
    print('Allocated ${count.value} bytes at ${buffer.address}');
    print('Name: ${name.toDartString()}');

    // No manual free needed -- arena handles it.
  });
}
```

### 5.3 Preventing Leaks with Finalizers

Attach a `NativeFinalizer` to a Dart object so native memory is freed
when the Dart object is garbage-collected:

```dart
import 'dart:ffi' as ffi;
import 'package:ffi/ffi.dart';

// Pointer to the C `free` function.
final _nativeFree = ffi.DynamicLibrary.process()
    .lookup<ffi.NativeFunction<ffi.Void Function(ffi.Pointer<ffi.Void>)>>(
      'free',
    );

final _finalizer = ffi.NativeFinalizer(_nativeFree);

class NativeBuffer implements ffi.Finalizable {
  NativeBuffer(int size) : pointer = calloc<ffi.Uint8>(size), length = size {
    _finalizer.attach(this, pointer.cast(), detach: this);
  }

  final ffi.Pointer<ffi.Uint8> pointer;
  final int length;

  /// Explicitly free before GC if you want deterministic cleanup.
  void dispose() {
    _finalizer.detach(this);
    calloc.free(pointer);
  }
}
```

---

## 6. Callbacks from Native to Dart

Pass Dart functions as C function pointers using `NativeCallable`:

```c
// C header
typedef void (*ProgressCallback)(int32_t percent);
void long_running_task(ProgressCallback callback);
```

```dart
import 'dart:ffi' as ffi;

typedef ProgressCallbackNative = ffi.Void Function(ffi.Int32 percent);
typedef LongRunningTaskNative = ffi.Void Function(
  ffi.Pointer<ffi.NativeFunction<ProgressCallbackNative>> callback,
);
typedef LongRunningTaskDart = void Function(
  ffi.Pointer<ffi.NativeFunction<ProgressCallbackNative>> callback,
);

class TaskRunner {
  TaskRunner() : _lib = loadNativeLibrary();

  final ffi.DynamicLibrary _lib;

  late final LongRunningTaskDart _longRunningTask = _lib
      .lookupFunction<LongRunningTaskNative, LongRunningTaskDart>(
        'long_running_task',
      );

  /// Runs a native task and reports progress via [onProgress].
  void runWithProgress(void Function(int percent) onProgress) {
    // NativeCallable.listener allows calls from any native thread.
    final callback = ffi.NativeCallable<ProgressCallbackNative>.listener(
      (int percent) {
        onProgress(percent);
        if (percent >= 100) {
          // Close when done to prevent leaks.
          // Note: this is handled after the callback returns.
        }
      },
    );

    _longRunningTask(callback.nativeFunction);

    // In production, close the callback when the task signals completion.
    // callback.close();
  }

  /// Synchronous callback variant (called on the same thread).
  void runWithSyncProgress(void Function(int percent) onProgress) {
    final callback =
        ffi.NativeCallable<ProgressCallbackNative>.isolateLocal(
          (int percent) => onProgress(percent),
        );

    _longRunningTask(callback.nativeFunction);
    callback.close();
  }
}
```

---

## 7. ffigen for Binding Generation

`package:ffigen` automatically generates Dart FFI bindings from C header files.

### 7.1 Configuration

Create `ffigen.yaml` in your project root:

```yaml
# ffigen.yaml
name: ImageProcessingBindings
description: Auto-generated bindings for image_processing.h
output: lib/src/bindings/image_processing_bindings.dart
headers:
  entry-points:
    - 'native/include/image_processing.h'
  include-directives:
    - 'native/include/image_processing.h'
compiler-opts:
  - '-Inative/include'
preamble: |
  // AUTO-GENERATED FILE. DO NOT EDIT.
  // Generated by package:ffigen.
type-map:
  typedefs:
    size_t:
      lib: 'ffi'
      c-type: 'Size'
      dart-type: 'int'
```

### 7.2 Running ffigen

```bash
# Install ffigen
dart pub add --dev ffigen

# Ensure LLVM/libclang is available
# macOS: xcode-select --install
# Linux: sudo apt install libclang-dev
# Windows: choco install llvm

# Generate bindings
dart run ffigen
```

### 7.3 Using Generated Bindings

```dart
import 'dart:ffi' as ffi;
import 'src/bindings/image_processing_bindings.dart';

class ImageService {
  ImageService()
      : _bindings = ImageProcessingBindings(loadNativeLibrary());

  final ImageProcessingBindings _bindings;

  int applyGrayscale(ffi.Pointer<ffi.Uint8> pixels, int length) {
    return _bindings.apply_grayscale(pixels, length);
  }

  double computeBrightness(ffi.Pointer<ffi.Uint8> pixels, int length) {
    return _bindings.compute_brightness(pixels, length);
  }
}
```

---

## 8. Platform-Specific Library Loading

A robust pattern for loading native libraries in a Flutter plugin:

```dart
import 'dart:ffi' as ffi;
import 'dart:io' show Platform;

import 'package:flutter/foundation.dart' show kIsWeb;

/// Singleton native library loader with lazy initialization.
class NativeLibraryLoader {
  NativeLibraryLoader._();

  static final instance = NativeLibraryLoader._();

  late final ffi.DynamicLibrary library = _load();

  ffi.DynamicLibrary _load() {
    if (kIsWeb) {
      throw UnsupportedError(
        'dart:ffi is not available on the web. '
        'Use JS interop or platform channels instead.',
      );
    }

    const libName = 'my_native_lib';

    if (Platform.isAndroid) {
      return ffi.DynamicLibrary.open('lib$libName.so');
    }
    if (Platform.isIOS) {
      // iOS: static framework linked into the runner.
      return ffi.DynamicLibrary.process();
    }
    if (Platform.isMacOS) {
      // macOS: look for the dylib inside the app bundle.
      return ffi.DynamicLibrary.open('lib$libName.dylib');
    }
    if (Platform.isWindows) {
      return ffi.DynamicLibrary.open('$libName.dll');
    }
    if (Platform.isLinux) {
      return ffi.DynamicLibrary.open('lib$libName.so');
    }
    throw UnsupportedError(
      'Unsupported platform: ${Platform.operatingSystem}',
    );
  }
}
```

### Flutter Plugin CMakeLists.txt (Linux/Windows)

```cmake
# linux/CMakeLists.txt
cmake_minimum_required(VERSION 3.13)
set(PROJECT_NAME "my_native_lib")
project(${PROJECT_NAME} LANGUAGES C)

add_library(${PROJECT_NAME} SHARED
  "../native/src/image_processing.c"
)

target_include_directories(${PROJECT_NAME} PRIVATE
  "../native/include"
)

set_target_properties(${PROJECT_NAME} PROPERTIES
  OUTPUT_NAME "${PROJECT_NAME}"
  C_STANDARD 11
)

# Install the library into the Flutter plugin's bundled library directory.
install(TARGETS ${PROJECT_NAME}
  LIBRARY DESTINATION "${CMAKE_INSTALL_PREFIX}/lib"
)
```

### Podspec for iOS/macOS

```ruby
# ios/my_native_lib.podspec
Pod::Spec.new do |s|
  s.name             = 'my_native_lib'
  s.version          = '1.0.0'
  s.summary          = 'Native image processing library.'
  s.homepage         = 'https://example.com'
  s.license          = { :type => 'MIT' }
  s.author           = 'Example'
  s.source           = { :path => '.' }

  s.source_files     = 'Classes/**/*', '../native/src/**/*.c', '../native/include/**/*.h'
  s.public_header_files = '../native/include/**/*.h'

  s.ios.deployment_target = '13.0'
  s.osx.deployment_target = '10.15'

  s.pod_target_xcconfig = {
    'DEFINES_MODULE' => 'YES',
    'HEADER_SEARCH_PATHS' => '"$(PODS_TARGET_SRCROOT)/../native/include"',
  }

  s.dependency 'Flutter'
  s.swift_version = '5.9'
end
```
