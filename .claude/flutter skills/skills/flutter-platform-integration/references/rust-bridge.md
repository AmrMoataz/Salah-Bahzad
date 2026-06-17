# Flutter Rust Bridge Guide

Flutter Rust Bridge (FRB) generates type-safe Dart bindings for Rust code,
enabling high-performance native computation with full async support, automatic
memory management, and rich type translation. It eliminates the manual FFI
boilerplate required when calling Rust from Dart.

---

## 1. Setup and Configuration

### 1.1 Prerequisites

```bash
# Install Rust (if not already installed)
curl --proto '=https' --tlsv1.2 -sSf https://sh.rustup.rs | sh

# Add the Flutter Rust Bridge CLI
cargo install flutter_rust_bridge_codegen

# Create a new Flutter project with Rust integration
flutter_rust_bridge_codegen create my_app
```

### 1.2 Adding to an Existing Flutter Project

```bash
# Initialize FRB in an existing Flutter project
cd my_flutter_app
flutter_rust_bridge_codegen integrate
```

This creates the following structure:

```
my_flutter_app/
  rust/
    Cargo.toml
    src/
      api/         # Your Rust API functions go here
        mod.rs
        simple.rs
      frb_generated.rs  # Auto-generated bridge code
      lib.rs
  lib/
    src/
      rust/
        api/
          simple.dart        # Auto-generated Dart bindings
        frb_generated.dart   # Auto-generated bridge setup
```

### 1.3 Cargo.toml

```toml
[package]
name = "rust_lib"
version = "0.1.0"
edition = "2021"

[lib]
crate-type = ["cdylib", "staticlib"]

[dependencies]
flutter_rust_bridge = "=2.9.0"
tokio = { version = "1", features = ["rt", "macros"] }
anyhow = "1"
```

### 1.4 Running Code Generation

```bash
# Regenerate bindings after changing Rust code
flutter_rust_bridge_codegen generate
```

---

## 2. Generating Dart Bindings from Rust

### 2.1 Defining the Rust API

Place public functions in `rust/src/api/`. FRB generates Dart bindings for
every `pub fn` in files under this directory.

```rust
// rust/src/api/math.rs

/// Adds two numbers. Synchronous, runs on the Dart isolate.
#[flutter_rust_bridge::frb(sync)]
pub fn add(a: i64, b: i64) -> i64 {
    a + b
}

/// Computes the nth Fibonacci number. Async by default --
/// runs on a Rust thread pool without blocking the Dart UI thread.
pub fn fibonacci(n: u32) -> u64 {
    match n {
        0 => 0,
        1 => 1,
        _ => {
            let (mut a, mut b) = (0u64, 1u64);
            for _ in 2..=n {
                let temp = a + b;
                a = b;
                b = temp;
            }
            b
        }
    }
}
```

Register the module in `rust/src/api/mod.rs`:

```rust
pub mod math;
```

### 2.2 Using Generated Bindings in Dart

```dart
import 'package:my_app/src/rust/api/math.dart';
import 'package:my_app/src/rust/frb_generated.dart';

Future<void> main() async {
  // Initialize the Rust bridge once at app startup.
  await RustLib.init();

  // Synchronous call (runs on the current isolate).
  final sum = add(a: 3, b: 5);
  print('3 + 5 = $sum'); // 8

  // Asynchronous call (runs on a Rust thread pool).
  final fib = await fibonacci(n: 50);
  print('fibonacci(50) = $fib'); // 12586269025
}
```

---

## 3. Async Rust Functions from Dart

By default, every Rust function without `#[frb(sync)]` runs asynchronously.
FRB dispatches the call to a Rust thread pool and returns a Dart `Future`.

```rust
// rust/src/api/image.rs

use std::path::PathBuf;
use std::fs;

/// Loads an image from disk, applies a blur filter, and saves the result.
/// This runs on a background Rust thread -- no UI jank.
pub fn blur_image(input_path: String, output_path: String, radius: f32) -> Result<u64, String> {
    let input = PathBuf::from(&input_path);
    let output = PathBuf::from(&output_path);

    let img = image::open(&input)
        .map_err(|e| format!("Failed to open image: {e}"))?;

    let blurred = img.blur(radius);

    blurred
        .save(&output)
        .map_err(|e| format!("Failed to save image: {e}"))?;

    let metadata = fs::metadata(&output)
        .map_err(|e| format!("Failed to read metadata: {e}"))?;

    Ok(metadata.len())
}

/// CPU-intensive hash computation.
pub fn compute_sha256(data: Vec<u8>) -> Vec<u8> {
    use std::collections::hash_map::DefaultHasher;
    use std::hash::{Hash, Hasher};
    // Simplified example -- in production, use the `sha2` crate.
    let mut hasher = DefaultHasher::new();
    data.hash(&mut hasher);
    hasher.finish().to_le_bytes().to_vec()
}
```

```dart
import 'package:my_app/src/rust/api/image.dart';

Future<void> processImage() async {
  try {
    final bytes = await blurImage(
      inputPath: '/path/to/input.png',
      outputPath: '/path/to/output.png',
      radius: 5.0,
    );
    print('Blurred image saved: $bytes bytes');
  } catch (e) {
    print('Image processing failed: $e');
  }
}
```

### Using Tokio Async in Rust

For IO-bound async work, use Tokio:

```rust
// rust/src/api/network.rs

/// Fetches a URL and returns the response body.
/// Uses tokio async runtime under the hood.
pub async fn fetch_url(url: String) -> Result<String, String> {
    let response = reqwest::get(&url)
        .await
        .map_err(|e| format!("Request failed: {e}"))?;

    let body = response
        .text()
        .await
        .map_err(|e| format!("Failed to read body: {e}"))?;

    Ok(body)
}
```

---

## 4. Struct Passing

FRB automatically translates Rust structs to Dart classes.

```rust
// rust/src/api/models.rs

/// A 2D point. FRB generates a Dart class with the same fields.
pub struct Point {
    pub x: f64,
    pub y: f64,
}

/// A polygon defined by its vertices.
pub struct Polygon {
    pub vertices: Vec<Point>,
    pub name: String,
}

/// Computes the area of a polygon using the shoelace formula.
pub fn polygon_area(polygon: Polygon) -> f64 {
    let n = polygon.vertices.len();
    if n < 3 {
        return 0.0;
    }

    let mut area = 0.0;
    for i in 0..n {
        let j = (i + 1) % n;
        area += polygon.vertices[i].x * polygon.vertices[j].y;
        area -= polygon.vertices[j].x * polygon.vertices[i].y;
    }
    area.abs() / 2.0
}

/// Translates a point by the given offset.
#[flutter_rust_bridge::frb(sync)]
pub fn translate_point(point: Point, dx: f64, dy: f64) -> Point {
    Point {
        x: point.x + dx,
        y: point.y + dy,
    }
}
```

```dart
import 'package:my_app/src/rust/api/models.dart';

Future<void> geometryExample() async {
  final triangle = Polygon(
    vertices: [
      Point(x: 0, y: 0),
      Point(x: 4, y: 0),
      Point(x: 0, y: 3),
    ],
    name: 'right-triangle',
  );

  final area = await polygonArea(polygon: triangle);
  print('Area: $area'); // 6.0

  // Synchronous struct return.
  final moved = translatePoint(point: Point(x: 1, y: 2), dx: 10, dy: 20);
  print('Moved to: (${moved.x}, ${moved.y})'); // (11.0, 22.0)
}
```

### Enums

```rust
// rust/src/api/models.rs

pub enum ImageFormat {
    Png,
    Jpeg { quality: u8 },
    WebP { lossless: bool },
}

pub fn format_extension(format: ImageFormat) -> String {
    match format {
        ImageFormat::Png => "png".to_string(),
        ImageFormat::Jpeg { .. } => "jpg".to_string(),
        ImageFormat::WebP { .. } => "webp".to_string(),
    }
}
```

```dart
// Generated Dart sealed class hierarchy.
final ext = await formatExtension(
  format: ImageFormat.jpeg(quality: 85),
);
print(ext); // "jpg"
```

---

## 5. Error Handling

### 5.1 Result-Based Errors

Return `Result<T, E>` from Rust. FRB converts `Err` variants into Dart
exceptions.

```rust
// rust/src/api/validation.rs

use anyhow::{bail, Context, Result};

/// Parses and validates an email address.
pub fn validate_email(email: String) -> Result<String> {
    if email.is_empty() {
        bail!("Email cannot be empty");
    }

    let parts: Vec<&str> = email.split('@').collect();
    if parts.len() != 2 {
        bail!("Email must contain exactly one '@' symbol");
    }

    let (local, domain) = (parts[0], parts[1]);

    if local.is_empty() {
        bail!("Local part cannot be empty");
    }
    if !domain.contains('.') {
        bail!("Domain must contain at least one '.'");
    }

    Ok(email.to_lowercase())
}

/// Custom error type for structured error handling.
#[derive(Debug)]
pub enum AppError {
    NotFound { resource: String },
    PermissionDenied { action: String },
    InvalidInput { field: String, reason: String },
}

impl std::fmt::Display for AppError {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        match self {
            AppError::NotFound { resource } => write!(f, "Not found: {resource}"),
            AppError::PermissionDenied { action } => {
                write!(f, "Permission denied: {action}")
            }
            AppError::InvalidInput { field, reason } => {
                write!(f, "Invalid input for '{field}': {reason}")
            }
        }
    }
}

impl std::error::Error for AppError {}

pub fn process_record(id: u64) -> Result<String, AppError> {
    if id == 0 {
        return Err(AppError::InvalidInput {
            field: "id".to_string(),
            reason: "must be non-zero".to_string(),
        });
    }
    if id > 1000 {
        return Err(AppError::NotFound {
            resource: format!("record/{id}"),
        });
    }
    Ok(format!("Processed record {id}"))
}
```

```dart
import 'package:my_app/src/rust/api/validation.dart';

Future<void> handleErrors() async {
  // anyhow::Error becomes a generic FrbAnyhowException.
  try {
    final email = await validateEmail(email: 'bad-email');
    print('Valid: $email');
  } catch (e) {
    print('Validation failed: $e');
  }

  // Custom error enums become Dart sealed classes.
  try {
    final result = await processRecord(id: 9999);
    print(result);
  } on AppError_NotFound catch (e) {
    print('Not found: ${e.resource}');
  } on AppError_PermissionDenied catch (e) {
    print('Denied: ${e.action}');
  } on AppError_InvalidInput catch (e) {
    print('Invalid ${e.field}: ${e.reason}');
  }
}
```

---

## 6. Streaming Data from Rust

Use `StreamSink` to push a continuous stream of values from Rust to Dart.

```rust
// rust/src/api/streaming.rs

use flutter_rust_bridge::frb;
use std::time::Duration;

/// Streams download progress events to Dart.
pub fn download_with_progress(
    url: String,
    sink: StreamSink<DownloadProgress>,
) -> Result<(), String> {
    // Simulated download in chunks.
    let total_bytes: u64 = 10_000_000;
    let chunk_size: u64 = 500_000;
    let mut downloaded: u64 = 0;

    while downloaded < total_bytes {
        std::thread::sleep(Duration::from_millis(50));
        downloaded = (downloaded + chunk_size).min(total_bytes);

        let progress = DownloadProgress {
            downloaded_bytes: downloaded,
            total_bytes,
            percent: (downloaded as f64 / total_bytes as f64 * 100.0) as u8,
        };

        sink.add(progress)
            .map_err(|e| format!("Stream closed: {e}"))?;
    }

    Ok(())
}

pub struct DownloadProgress {
    pub downloaded_bytes: u64,
    pub total_bytes: u64,
    pub percent: u8,
}

/// Streams sensor readings at a fixed interval.
pub fn stream_sensor_data(
    interval_ms: u64,
    sink: StreamSink<SensorEvent>,
) {
    let mut counter = 0u64;
    loop {
        std::thread::sleep(Duration::from_millis(interval_ms));
        counter += 1;

        let event = SensorEvent {
            id: counter,
            temperature: 20.0 + (counter as f64 * 0.1).sin() * 5.0,
            humidity: 50.0 + (counter as f64 * 0.05).cos() * 10.0,
        };

        if sink.add(event).is_err() {
            // Dart cancelled the stream.
            break;
        }
    }
}

pub struct SensorEvent {
    pub id: u64,
    pub temperature: f64,
    pub humidity: f64,
}
```

```dart
import 'package:my_app/src/rust/api/streaming.dart';

void listenToDownload() {
  downloadWithProgress(url: 'https://example.com/file.bin').listen(
    (progress) {
      print('${progress.percent}% '
          '(${progress.downloadedBytes}/${progress.totalBytes})');
    },
    onDone: () => print('Download complete'),
    onError: (e) => print('Download failed: $e'),
  );
}

void listenToSensors() {
  final subscription = streamSensorData(intervalMs: 100).listen(
    (event) {
      print('Sensor #${event.id}: '
          'temp=${event.temperature.toStringAsFixed(1)} '
          'humidity=${event.humidity.toStringAsFixed(1)}');
    },
  );

  // Cancel after 10 seconds.
  Future.delayed(Duration(seconds: 10), subscription.cancel);
}
```

---

## 7. Performance Benefits

### When Rust Outperforms Dart

| Scenario | Dart | Rust | Speedup |
|---|---|---|---|
| JSON parsing (100MB) | ~800ms | ~120ms | ~6.5x |
| Image blur (4K) | ~2200ms | ~180ms | ~12x |
| SHA-256 (1GB) | ~4500ms | ~600ms | ~7.5x |
| Regex over 10M strings | ~3000ms | ~400ms | ~7.5x |

These are illustrative benchmarks. Actual results depend on hardware, data
characteristics, and implementation quality.

### Why Rust is Fast in Flutter

1. **No GC pauses.** Rust has zero-cost memory management via ownership.
2. **SIMD and vectorization.** The Rust compiler aggressively auto-vectorizes.
3. **True parallelism.** Rust threads run in parallel without a GIL or isolate
   boundary overhead.
4. **Zero-copy across FFI.** Typed data can be shared between Dart and Rust
   without copying when using `ZeroCopyBuffer`.

### Zero-Copy Optimization

```rust
// rust/src/api/zero_copy.rs

use flutter_rust_bridge::ZeroCopyBuffer;

/// Returns large data without copying from Rust to Dart.
pub fn generate_texture(width: u32, height: u32) -> ZeroCopyBuffer<Vec<u8>> {
    let size = (width * height * 4) as usize; // RGBA
    let mut buffer = Vec::with_capacity(size);

    for y in 0..height {
        for x in 0..width {
            buffer.push((x % 256) as u8);        // R
            buffer.push((y % 256) as u8);        // G
            buffer.push(((x + y) % 256) as u8);  // B
            buffer.push(255);                      // A
        }
    }

    ZeroCopyBuffer(buffer)
}
```

---

## 8. When to Use Rust vs Native (Swift/Kotlin)

| Criterion | Use Rust | Use Swift/Kotlin |
|---|---|---|
| **CPU-bound computation** | Preferred: image processing, crypto, compression, ML inference | Not ideal for heavy computation |
| **Cross-platform shared logic** | Single Rust crate compiles for iOS, Android, desktop, web (WASM) | Must write separately for each platform |
| **Platform-specific SDK access** | Cannot directly call Apple/Android SDKs | Required for HealthKit, ARKit, Google Play Services, etc. |
| **Existing C/C++ libraries** | Rust FFI to C is straightforward | Use JNI (Android) or bridging headers (iOS) |
| **Async I/O** | Tokio ecosystem is excellent | Native async/await is also excellent |
| **Team expertise** | Requires Rust knowledge | Most mobile teams already know Swift/Kotlin |
| **Binary size** | Adds ~2-5MB per platform | Minimal: uses system frameworks |
| **Prototyping speed** | Slower: stricter compiler | Faster: more forgiving type systems |
| **Memory safety** | Compile-time guaranteed | Runtime-checked (ARC / GC) |

### Decision Framework

Use **Rust** when:
- The same algorithm must run identically on all platforms.
- You need maximum throughput for CPU-intensive operations.
- You have an existing Rust or C/C++ library to wrap.
- Memory safety and concurrency correctness are critical.

Use **Swift/Kotlin via platform channels** when:
- You need access to platform-specific SDKs (HealthKit, ARKit, CameraX).
- The feature is UI-related and platform-specific.
- The native implementation is trivial (a few lines of SDK calls).
- You want the smallest possible binary size impact.

Use **both** when:
- Core computation happens in Rust.
- A thin Swift/Kotlin layer calls platform SDKs and feeds data to Rust.
- Example: A fitness app where HealthKit (Swift) provides raw data, Rust
  processes and analyzes it, and the results are streamed to Dart.
