# Platform Channels Guide

Platform channels are the primary mechanism for communication between Dart and
native host code. Flutter provides three channel types, each suited to a
different communication pattern.

---

## 1. MethodChannel -- Request/Response Communication

`MethodChannel` implements a bidirectional RPC-style protocol. Dart can invoke
methods on the native side, and native code can invoke methods on the Dart side.
Every call returns a `Future` on Dart or a callback on native.

### 1.1 Dart Side Setup

```dart
import 'package:flutter/services.dart';

/// A service that communicates with native battery APIs.
class BatteryService {
  // Use a reverse-domain channel name to avoid collisions.
  static const _channel = MethodChannel('com.example.app/battery');

  /// Returns the current battery level as a percentage (0-100).
  Future<int> getBatteryLevel() async {
    try {
      final int level = await _channel.invokeMethod<int>('getBatteryLevel') ?? -1;
      return level;
    } on PlatformException catch (e) {
      throw BatteryException('Failed to get battery level: ${e.message}');
    } on MissingPluginException {
      throw BatteryException('Battery plugin not available on this platform.');
    }
  }

  /// Sets the low-battery warning threshold on the native side.
  Future<void> setLowBatteryThreshold(int percent) async {
    try {
      await _channel.invokeMethod<void>(
        'setLowBatteryThreshold',
        {'percent': percent},
      );
    } on PlatformException catch (e) {
      throw BatteryException('Failed to set threshold: ${e.message}');
    }
  }
}

class BatteryException implements Exception {
  BatteryException(this.message);
  final String message;

  @override
  String toString() => 'BatteryException: $message';
}
```

### 1.2 iOS Implementation (Swift)

```swift
import Flutter
import UIKit

public class BatteryPlugin: NSObject, FlutterPlugin {

    public static func register(with registrar: FlutterPluginRegistrar) {
        let channel = FlutterMethodChannel(
            name: "com.example.app/battery",
            binaryMessenger: registrar.messenger()
        )
        let instance = BatteryPlugin()
        registrar.addMethodCallDelegate(instance, channel: channel)
    }

    public func handle(
        _ call: FlutterMethodCall,
        result: @escaping FlutterResult
    ) {
        switch call.method {
        case "getBatteryLevel":
            handleGetBatteryLevel(result: result)
        case "setLowBatteryThreshold":
            handleSetThreshold(call: call, result: result)
        default:
            result(FlutterMethodNotImplemented)
        }
    }

    private func handleGetBatteryLevel(result: @escaping FlutterResult) {
        let device = UIDevice.current
        device.isBatteryMonitoringEnabled = true
        let level = device.batteryLevel
        if level < 0 {
            result(
                FlutterError(
                    code: "UNAVAILABLE",
                    message: "Battery level not available on this device.",
                    details: nil
                )
            )
        } else {
            result(Int(level * 100))
        }
    }

    private func handleSetThreshold(
        call: FlutterMethodCall,
        result: @escaping FlutterResult
    ) {
        guard
            let args = call.arguments as? [String: Any],
            let percent = args["percent"] as? Int
        else {
            result(
                FlutterError(
                    code: "BAD_ARGS",
                    message: "Expected 'percent' as Int.",
                    details: nil
                )
            )
            return
        }
        // Store threshold in native layer (UserDefaults, etc.)
        UserDefaults.standard.set(percent, forKey: "lowBatteryThreshold")
        result(nil) // void return
    }
}
```

### 1.3 Android Implementation (Kotlin)

```kotlin
package com.example.app

import android.content.Context
import android.os.BatteryManager
import io.flutter.embedding.engine.plugins.FlutterPlugin
import io.flutter.plugin.common.MethodCall
import io.flutter.plugin.common.MethodChannel
import io.flutter.plugin.common.MethodChannel.MethodCallHandler
import io.flutter.plugin.common.MethodChannel.Result

class BatteryPlugin : FlutterPlugin, MethodCallHandler {

    private lateinit var channel: MethodChannel
    private lateinit var context: Context

    override fun onAttachedToEngine(binding: FlutterPlugin.FlutterPluginBinding) {
        context = binding.applicationContext
        channel = MethodChannel(binding.binaryMessenger, "com.example.app/battery")
        channel.setMethodCallHandler(this)
    }

    override fun onDetachedFromEngine(binding: FlutterPlugin.FlutterPluginBinding) {
        channel.setMethodCallHandler(null)
    }

    override fun onMethodCall(call: MethodCall, result: Result) {
        when (call.method) {
            "getBatteryLevel" -> handleGetBatteryLevel(result)
            "setLowBatteryThreshold" -> handleSetThreshold(call, result)
            else -> result.notImplemented()
        }
    }

    private fun handleGetBatteryLevel(result: Result) {
        val batteryManager =
            context.getSystemService(Context.BATTERY_SERVICE) as? BatteryManager
        if (batteryManager == null) {
            result.error("UNAVAILABLE", "Battery manager not available.", null)
            return
        }
        val level = batteryManager.getIntProperty(BatteryManager.BATTERY_PROPERTY_CAPACITY)
        if (level < 0) {
            result.error("UNAVAILABLE", "Battery level not available.", null)
        } else {
            result.success(level)
        }
    }

    private fun handleSetThreshold(call: MethodCall, result: Result) {
        val percent = call.argument<Int>("percent")
        if (percent == null) {
            result.error("BAD_ARGS", "Expected 'percent' as Int.", null)
            return
        }
        context.getSharedPreferences("battery_prefs", Context.MODE_PRIVATE)
            .edit()
            .putInt("lowBatteryThreshold", percent)
            .apply()
        result.success(null)
    }
}
```

---

## 2. Type Codecs -- StandardMessageCodec Supported Types

`StandardMessageCodec` is the default codec for `MethodChannel`. It supports a
fixed set of types that map between Dart and native:

| Dart Type | Android (Java/Kotlin) | iOS (Swift/ObjC) |
|---|---|---|
| `null` | `null` | `nil` (NSNull) |
| `bool` | `Boolean` | `NSNumber(boolValue:)` |
| `int` (fits 32-bit) | `Int` | `NSNumber(value: Int32)` |
| `int` (fits 64-bit) | `Long` | `NSNumber(value: Int64)` |
| `double` | `Double` | `NSNumber(value: Double)` |
| `String` | `String` | `String` |
| `Uint8List` | `byte[]` | `FlutterStandardTypedData(bytes:)` |
| `Int32List` | `int[]` | `FlutterStandardTypedData(int32:)` |
| `Int64List` | `long[]` | `FlutterStandardTypedData(int64:)` |
| `Float32List` | `float[]` | `FlutterStandardTypedData(float32:)` |
| `Float64List` | `double[]` | `FlutterStandardTypedData(float64:)` |
| `List` | `ArrayList` | `NSArray` |
| `Map` | `HashMap` | `NSDictionary` |

**Important:** Only these types cross the boundary. For complex domain objects,
serialize to `Map<String, dynamic>` or use JSON encoding before sending.

---

## 3. EventChannel -- Continuous Data Streams

`EventChannel` creates a long-lived stream from native code to Dart. Native code
pushes events; Dart receives them as a `Stream`.

### 3.1 Dart Side

```dart
import 'package:flutter/services.dart';

class AccelerometerService {
  static const _eventChannel =
      EventChannel('com.example.app/accelerometer');

  /// Returns a broadcast stream of accelerometer readings.
  /// Each event is a Map with keys 'x', 'y', 'z'.
  Stream<({double x, double y, double z})> get readings {
    return _eventChannel.receiveBroadcastStream().map((event) {
      final map = Map<String, double>.from(event as Map);
      return (x: map['x']!, y: map['y']!, z: map['z']!);
    });
  }
}
```

### 3.2 iOS EventChannel (Swift)

```swift
import Flutter
import CoreMotion

public class AccelerometerPlugin: NSObject, FlutterPlugin, FlutterStreamHandler {

    private let motionManager = CMMotionManager()
    private var eventSink: FlutterEventSink?

    public static func register(with registrar: FlutterPluginRegistrar) {
        let channel = FlutterEventChannel(
            name: "com.example.app/accelerometer",
            binaryMessenger: registrar.messenger()
        )
        let instance = AccelerometerPlugin()
        channel.setStreamHandler(instance)
    }

    public func onListen(
        withArguments arguments: Any?,
        eventSink events: @escaping FlutterEventSink
    ) -> FlutterError? {
        self.eventSink = events

        guard motionManager.isAccelerometerAvailable else {
            events(
                FlutterError(
                    code: "UNAVAILABLE",
                    message: "Accelerometer not available.",
                    details: nil
                )
            )
            return nil
        }

        motionManager.accelerometerUpdateInterval = 1.0 / 60.0
        motionManager.startAccelerometerUpdates(to: .main) { data, error in
            if let error = error {
                events(
                    FlutterError(
                        code: "SENSOR_ERROR",
                        message: error.localizedDescription,
                        details: nil
                    )
                )
                return
            }
            if let data = data {
                events([
                    "x": data.acceleration.x,
                    "y": data.acceleration.y,
                    "z": data.acceleration.z,
                ])
            }
        }
        return nil
    }

    public func onCancel(withArguments arguments: Any?) -> FlutterError? {
        motionManager.stopAccelerometerUpdates()
        eventSink = nil
        return nil
    }
}
```

### 3.3 Android EventChannel (Kotlin)

```kotlin
package com.example.app

import android.content.Context
import android.hardware.Sensor
import android.hardware.SensorEvent
import android.hardware.SensorEventListener
import android.hardware.SensorManager
import io.flutter.embedding.engine.plugins.FlutterPlugin
import io.flutter.plugin.common.EventChannel

class AccelerometerPlugin : FlutterPlugin, EventChannel.StreamHandler {

    private lateinit var sensorManager: SensorManager
    private var listener: SensorEventListener? = null

    override fun onAttachedToEngine(binding: FlutterPlugin.FlutterPluginBinding) {
        sensorManager = binding.applicationContext
            .getSystemService(Context.SENSOR_SERVICE) as SensorManager
        val channel = EventChannel(
            binding.binaryMessenger,
            "com.example.app/accelerometer"
        )
        channel.setStreamHandler(this)
    }

    override fun onDetachedFromEngine(binding: FlutterPlugin.FlutterPluginBinding) {}

    override fun onListen(arguments: Any?, events: EventChannel.EventSink) {
        val accelerometer =
            sensorManager.getDefaultSensor(Sensor.TYPE_ACCELEROMETER)
        if (accelerometer == null) {
            events.error("UNAVAILABLE", "Accelerometer not available.", null)
            return
        }
        listener = object : SensorEventListener {
            override fun onSensorChanged(event: SensorEvent) {
                events.success(
                    mapOf(
                        "x" to event.values[0].toDouble(),
                        "y" to event.values[1].toDouble(),
                        "z" to event.values[2].toDouble(),
                    )
                )
            }

            override fun onAccuracyChanged(sensor: Sensor?, accuracy: Int) {}
        }
        sensorManager.registerListener(
            listener,
            accelerometer,
            SensorManager.SENSOR_DELAY_UI,
        )
    }

    override fun onCancel(arguments: Any?) {
        listener?.let { sensorManager.unregisterListener(it) }
        listener = null
    }
}
```

---

## 4. BasicMessageChannel

`BasicMessageChannel` is a lower-level channel for sending arbitrary messages
with a custom codec. Use it when you need a non-RPC communication style.

```dart
import 'package:flutter/services.dart';

class ConfigChannel {
  static const _channel = BasicMessageChannel<String>(
    'com.example.app/config',
    StringCodec(),
  );

  /// Sends a JSON config string to the native side and receives
  /// an acknowledgement string.
  Future<String?> sendConfig(String jsonConfig) async {
    final reply = await _channel.send(jsonConfig);
    return reply;
  }

  /// Listens for config updates pushed from the native side.
  void onNativeConfigUpdate(void Function(String? message) handler) {
    _channel.setMessageHandler((message) async {
      handler(message);
      return 'ack';
    });
  }
}
```

Available built-in codecs:

| Codec | Message Type | Use Case |
|---|---|---|
| `StringCodec` | `String` | Plain text, JSON strings |
| `BinaryCodec` | `ByteData` | Raw binary data |
| `JSONMessageCodec` | `dynamic` (JSON-compatible) | Structured JSON |
| `StandardMessageCodec` | `dynamic` (standard types) | Mixed typed data |

---

## 5. Error Handling Across Platforms

### 5.1 Native to Dart Error Propagation

On native, return errors through the result/event callback:

```swift
// iOS
result(FlutterError(code: "PERMISSION_DENIED", message: "Camera access denied.", details: nil))
```

```kotlin
// Android
result.error("PERMISSION_DENIED", "Camera access denied.", null)
```

On Dart, these arrive as `PlatformException`:

```dart
try {
  await channel.invokeMethod('openCamera');
} on PlatformException catch (e) {
  switch (e.code) {
    case 'PERMISSION_DENIED':
      // Show permission dialog
      break;
    case 'DEVICE_NOT_FOUND':
      // Show error UI
      break;
    default:
      // Log unexpected error
      rethrow;
  }
}
```

### 5.2 Comprehensive Error Wrapper

```dart
import 'package:flutter/services.dart';

/// A typed result from a platform call.
sealed class PlatformResult<T> {
  const PlatformResult();
}

final class PlatformSuccess<T> extends PlatformResult<T> {
  const PlatformSuccess(this.value);
  final T value;
}

final class PlatformFailure<T> extends PlatformResult<T> {
  const PlatformFailure(this.code, this.message, [this.details]);
  final String code;
  final String message;
  final Object? details;
}

/// Wraps a platform channel call into a [PlatformResult].
Future<PlatformResult<T>> safePlatformCall<T>(
  Future<T?> Function() call, {
  required T fallback,
}) async {
  try {
    final result = await call();
    return PlatformSuccess<T>(result ?? fallback);
  } on PlatformException catch (e) {
    return PlatformFailure<T>(
      e.code,
      e.message ?? 'Unknown platform error',
      e.details,
    );
  } on MissingPluginException {
    return PlatformFailure<T>(
      'MISSING_PLUGIN',
      'Plugin not registered on this platform.',
    );
  }
}
```

---

## 6. Testing Platform Channels

Use `TestDefaultBinaryMessengerBinding` to intercept channel calls in tests.

```dart
import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();

  const channel = MethodChannel('com.example.app/battery');

  setUp(() {
    TestDefaultBinaryMessengerBinding.instance.defaultBinaryMessenger
        .setMockMethodCallHandler(channel, (MethodCall call) async {
      switch (call.method) {
        case 'getBatteryLevel':
          return 85;
        case 'setLowBatteryThreshold':
          final args = call.arguments as Map;
          if (args['percent'] == null) {
            throw PlatformException(
              code: 'BAD_ARGS',
              message: 'percent is required',
            );
          }
          return null;
        default:
          return null;
      }
    });
  });

  tearDown(() {
    TestDefaultBinaryMessengerBinding.instance.defaultBinaryMessenger
        .setMockMethodCallHandler(channel, null);
  });

  test('getBatteryLevel returns mocked value', () async {
    final service = BatteryService();
    final level = await service.getBatteryLevel();
    expect(level, 85);
  });

  test('setLowBatteryThreshold completes without error', () async {
    final service = BatteryService();
    await expectLater(
      service.setLowBatteryThreshold(20),
      completes,
    );
  });
}
```

### Testing EventChannel Streams

```dart
import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();

  const eventChannel = EventChannel('com.example.app/accelerometer');
  const methodChannel = MethodChannel('com.example.app/accelerometer');

  setUp(() {
    // EventChannel uses a MethodChannel under the hood for listen/cancel.
    TestDefaultBinaryMessengerBinding.instance.defaultBinaryMessenger
        .setMockMethodCallHandler(methodChannel, (MethodCall call) async {
      if (call.method == 'listen') {
        // Simulate pushing events on the next microtask.
        Future.microtask(() async {
          final data = StandardMessageCodec()
              .encodeMessage(<String, double>{'x': 1.0, 'y': 2.0, 'z': 3.0});
          await TestDefaultBinaryMessengerBinding
              .instance.defaultBinaryMessenger
              .handlePlatformMessage(
            'com.example.app/accelerometer',
            data,
            (ByteData? reply) {},
          );
        });
        return null;
      }
      if (call.method == 'cancel') {
        return null;
      }
      return null;
    });
  });

  tearDown(() {
    TestDefaultBinaryMessengerBinding.instance.defaultBinaryMessenger
        .setMockMethodCallHandler(methodChannel, null);
  });

  test('accelerometer stream emits readings', () async {
    final service = AccelerometerService();
    final reading = await service.readings.first;
    expect(reading.x, 1.0);
    expect(reading.y, 2.0);
    expect(reading.z, 3.0);
  });
}
```
