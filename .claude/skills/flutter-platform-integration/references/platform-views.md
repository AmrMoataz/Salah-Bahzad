# Platform Views Guide

Platform views embed native iOS and Android views directly into the Flutter
widget tree. Use them when no Flutter equivalent exists for a native UI
component -- such as Google Maps, WebView, camera preview, or a native ad
banner.

---

## 1. AndroidView and UiKitView

Flutter provides two widgets for embedding platform views:

| Widget | Platform | Description |
|---|---|---|
| `AndroidView` | Android | Embeds an Android `View` in the Flutter widget tree |
| `UiKitView` | iOS | Embeds a UIKit `UIView` in the Flutter widget tree |

Both widgets require a registered `PlatformViewFactory` on the native side.

### 1.1 Basic Dart Usage

```dart
import 'dart:io' show Platform;

import 'package:flutter/foundation.dart';
import 'package:flutter/gestures.dart';
import 'package:flutter/material.dart';
import 'package:flutter/rendering.dart';
import 'package:flutter/services.dart';

/// A widget that displays a native map view.
class NativeMapView extends StatelessWidget {
  const NativeMapView({
    super.key,
    required this.initialLatitude,
    required this.initialLongitude,
    this.zoom = 14.0,
    this.onMapReady,
  });

  final double initialLatitude;
  final double initialLongitude;
  final double zoom;
  final VoidCallback? onMapReady;

  @override
  Widget build(BuildContext context) {
    const viewType = 'com.example.app/native-map';

    final creationParams = <String, dynamic>{
      'latitude': initialLatitude,
      'longitude': initialLongitude,
      'zoom': zoom,
    };

    if (Platform.isAndroid) {
      return PlatformViewLink(
        viewType: viewType,
        surfaceFactory: (context, controller) {
          return AndroidViewSurface(
            controller: controller as AndroidViewController,
            gestureRecognizers: const <Factory<OneSequenceGestureRecognizer>>{},
            hitTestBehavior: PlatformViewHitTestBehavior.opaque,
          );
        },
        onCreatePlatformView: (params) {
          return PlatformViewsService.initExpensiveAndroidView(
            id: params.id,
            viewType: viewType,
            layoutDirection: TextDirection.ltr,
            creationParams: creationParams,
            creationParamsCodec: const StandardMessageCodec(),
            onFocus: () => params.onFocusChanged(true),
          )
            ..addOnPlatformViewCreatedListener(params.onPlatformViewCreated)
            ..addOnPlatformViewCreatedListener((_) => onMapReady?.call())
            ..create();
        },
      );
    }

    if (Platform.isIOS) {
      return UiKitView(
        viewType: viewType,
        creationParams: creationParams,
        creationParamsCodec: const StandardMessageCodec(),
        onPlatformViewCreated: (_) => onMapReady?.call(),
        gestureRecognizers: const <Factory<OneSequenceGestureRecognizer>>{},
      );
    }

    return const Center(
      child: Text('Native map not supported on this platform.'),
    );
  }
}
```

---

## 2. Hybrid Composition Mode

Hybrid composition is the recommended rendering mode for platform views on
Android. It embeds the native view in the Flutter view hierarchy using
Android's `PlatformViewsService.initExpensiveAndroidView`.

### Advantages

- Correct rendering order: native views composite properly with Flutter widgets.
- Touch and gesture events dispatch correctly.
- Accessibility works for the native view.

### Disadvantages

- Thread synchronization between the platform UI thread and the raster thread
  adds overhead.
- Each platform view adds measurable rendering latency (typically 1-3ms per frame
  on mid-range devices).

### Android Registration (Kotlin)

```kotlin
package com.example.app

import android.content.Context
import android.view.View
import android.widget.FrameLayout
import io.flutter.embedding.engine.plugins.FlutterPlugin
import io.flutter.plugin.common.StandardMessageCodec
import io.flutter.plugin.platform.PlatformView
import io.flutter.plugin.platform.PlatformViewFactory

class NativeMapPlugin : FlutterPlugin {

    override fun onAttachedToEngine(binding: FlutterPlugin.FlutterPluginBinding) {
        binding.platformViewRegistry.registerViewFactory(
            "com.example.app/native-map",
            NativeMapViewFactory(),
        )
    }

    override fun onDetachedFromEngine(binding: FlutterPlugin.FlutterPluginBinding) {}
}

class NativeMapViewFactory : PlatformViewFactory(StandardMessageCodec.INSTANCE) {
    override fun create(context: Context, viewId: Int, args: Any?): PlatformView {
        val params = args as? Map<*, *> ?: emptyMap<String, Any>()
        return NativeMapPlatformView(context, viewId, params)
    }
}

class NativeMapPlatformView(
    context: Context,
    private val viewId: Int,
    private val params: Map<*, *>,
) : PlatformView {

    private val mapContainer: FrameLayout = FrameLayout(context).apply {
        // Initialize your native map SDK here.
        // val latitude = params["latitude"] as? Double ?: 0.0
        // val longitude = params["longitude"] as? Double ?: 0.0
        // val zoom = params["zoom"] as? Double ?: 14.0
    }

    override fun getView(): View = mapContainer

    override fun dispose() {
        // Release native map resources.
    }
}
```

---

## 3. Virtual Display Mode

Virtual display renders the native view into an off-screen texture that Flutter
composites. It uses `AndroidView` directly (or `initSurfaceAndroidView`).

### Characteristics

| Property | Virtual Display | Hybrid Composition |
|---|---|---|
| Rendering | Off-screen texture | Direct view hierarchy |
| Performance | Better frame rate | Slightly lower frame rate |
| Touch events | May have edge-case issues | Fully correct |
| Accessibility | Limited | Full |
| API level | All supported | Android 10+ for best results |

### When to Use Virtual Display

- The native view is non-interactive (e.g., a static video thumbnail).
- You need maximum rendering performance and can tolerate touch quirks.
- Targeting older Android versions where hybrid composition has issues.

```dart
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

/// Uses virtual display mode (texture-based rendering).
class VirtualDisplayMapView extends StatelessWidget {
  const VirtualDisplayMapView({super.key});

  @override
  Widget build(BuildContext context) {
    return AndroidView(
      viewType: 'com.example.app/native-map',
      creationParams: const <String, dynamic>{
        'latitude': 37.7749,
        'longitude': -122.4194,
        'zoom': 12.0,
      },
      creationParamsCodec: const StandardMessageCodec(),
    );
  }
}
```

---

## 4. Embedding Native Views

### 4.1 WebView

```dart
import 'dart:io' show Platform;

import 'package:flutter/foundation.dart';
import 'package:flutter/gestures.dart';
import 'package:flutter/material.dart';
import 'package:flutter/rendering.dart';
import 'package:flutter/services.dart';

class NativeWebView extends StatefulWidget {
  const NativeWebView({
    super.key,
    required this.initialUrl,
    this.onPageFinished,
  });

  final String initialUrl;
  final ValueChanged<String>? onPageFinished;

  @override
  State<NativeWebView> createState() => _NativeWebViewState();
}

class _NativeWebViewState extends State<NativeWebView> {
  late final MethodChannel _channel;

  void _onPlatformViewCreated(int viewId) {
    _channel = MethodChannel('com.example.app/webview_$viewId');
    _channel.setMethodCallHandler(_handleMethodCall);
  }

  Future<dynamic> _handleMethodCall(MethodCall call) async {
    switch (call.method) {
      case 'onPageFinished':
        widget.onPageFinished?.call(call.arguments as String);
      case 'onPageStarted':
        // Handle loading state.
        break;
    }
    return null;
  }

  /// Evaluates JavaScript in the web view and returns the result.
  Future<String?> evaluateJavascript(String script) async {
    final result = await _channel.invokeMethod<String>(
      'evaluateJavascript',
      script,
    );
    return result;
  }

  /// Navigates the web view to a new URL.
  Future<void> loadUrl(String url) async {
    await _channel.invokeMethod<void>('loadUrl', url);
  }

  @override
  Widget build(BuildContext context) {
    const viewType = 'com.example.app/webview';
    final creationParams = <String, dynamic>{
      'initialUrl': widget.initialUrl,
    };

    if (Platform.isAndroid) {
      return PlatformViewLink(
        viewType: viewType,
        surfaceFactory: (context, controller) {
          return AndroidViewSurface(
            controller: controller as AndroidViewController,
            gestureRecognizers: const <Factory<OneSequenceGestureRecognizer>>{},
            hitTestBehavior: PlatformViewHitTestBehavior.opaque,
          );
        },
        onCreatePlatformView: (params) {
          return PlatformViewsService.initExpensiveAndroidView(
            id: params.id,
            viewType: viewType,
            layoutDirection: TextDirection.ltr,
            creationParams: creationParams,
            creationParamsCodec: const StandardMessageCodec(),
          )
            ..addOnPlatformViewCreatedListener((id) {
              params.onPlatformViewCreated(id);
              _onPlatformViewCreated(id);
            })
            ..create();
        },
      );
    }

    if (Platform.isIOS) {
      return UiKitView(
        viewType: viewType,
        creationParams: creationParams,
        creationParamsCodec: const StandardMessageCodec(),
        onPlatformViewCreated: _onPlatformViewCreated,
      );
    }

    return const Center(child: Text('WebView not supported.'));
  }
}
```

### 4.2 iOS Registration (Swift)

```swift
import Flutter
import UIKit
import WebKit

public class WebViewPlugin: NSObject, FlutterPlugin {

    public static func register(with registrar: FlutterPluginRegistrar) {
        let factory = WebViewFactory(messenger: registrar.messenger())
        registrar.register(factory, withId: "com.example.app/webview")
    }
}

class WebViewFactory: NSObject, FlutterPlatformViewFactory {

    private let messenger: FlutterBinaryMessenger

    init(messenger: FlutterBinaryMessenger) {
        self.messenger = messenger
        super.init()
    }

    func create(
        withFrame frame: CGRect,
        viewIdentifier viewId: Int64,
        arguments args: Any?
    ) -> FlutterPlatformView {
        return WebViewPlatformView(
            frame: frame,
            viewId: viewId,
            args: args as? [String: Any] ?? [:],
            messenger: messenger
        )
    }

    func createArgsCodec() -> FlutterMessageCodec & NSObjectProtocol {
        return FlutterStandardMessageCodec.sharedInstance()
    }
}

class WebViewPlatformView: NSObject, FlutterPlatformView, WKNavigationDelegate {

    private let webView: WKWebView
    private let channel: FlutterMethodChannel

    init(
        frame: CGRect,
        viewId: Int64,
        args: [String: Any],
        messenger: FlutterBinaryMessenger
    ) {
        let config = WKWebViewConfiguration()
        config.allowsInlineMediaPlayback = true

        webView = WKWebView(frame: frame, configuration: config)
        channel = FlutterMethodChannel(
            name: "com.example.app/webview_\(viewId)",
            binaryMessenger: messenger
        )

        super.init()

        webView.navigationDelegate = self

        channel.setMethodCallHandler { [weak self] call, result in
            self?.handleMethodCall(call, result: result)
        }

        if let urlString = args["initialUrl"] as? String,
           let url = URL(string: urlString) {
            webView.load(URLRequest(url: url))
        }
    }

    func view() -> UIView { webView }

    private func handleMethodCall(
        _ call: FlutterMethodCall,
        result: @escaping FlutterResult
    ) {
        switch call.method {
        case "loadUrl":
            if let urlString = call.arguments as? String,
               let url = URL(string: urlString) {
                webView.load(URLRequest(url: url))
                result(nil)
            } else {
                result(FlutterError(
                    code: "BAD_URL", message: "Invalid URL.", details: nil
                ))
            }
        case "evaluateJavascript":
            if let script = call.arguments as? String {
                webView.evaluateJavaScript(script) { value, error in
                    if let error = error {
                        result(FlutterError(
                            code: "JS_ERROR",
                            message: error.localizedDescription,
                            details: nil
                        ))
                    } else {
                        result(value as? String)
                    }
                }
            }
        default:
            result(FlutterMethodNotImplemented)
        }
    }

    // MARK: - WKNavigationDelegate

    func webView(
        _ webView: WKWebView,
        didFinish navigation: WKNavigation!
    ) {
        channel.invokeMethod("onPageFinished", arguments: webView.url?.absoluteString)
    }

    func webView(
        _ webView: WKWebView,
        didStartProvisionalNavigation navigation: WKNavigation!
    ) {
        channel.invokeMethod("onPageStarted", arguments: webView.url?.absoluteString)
    }
}
```

### 4.3 Camera Preview

```dart
import 'dart:io' show Platform;

import 'package:flutter/foundation.dart';
import 'package:flutter/gestures.dart';
import 'package:flutter/material.dart';
import 'package:flutter/rendering.dart';
import 'package:flutter/services.dart';

/// Embeds a native camera preview with overlay support.
class NativeCameraPreview extends StatefulWidget {
  const NativeCameraPreview({
    super.key,
    this.facing = CameraFacing.back,
    this.onCameraReady,
  });

  final CameraFacing facing;
  final VoidCallback? onCameraReady;

  @override
  State<NativeCameraPreview> createState() => _NativeCameraPreviewState();
}

enum CameraFacing { front, back }

class _NativeCameraPreviewState extends State<NativeCameraPreview> {
  MethodChannel? _channel;

  void _onPlatformViewCreated(int viewId) {
    _channel = MethodChannel('com.example.app/camera_$viewId');
    widget.onCameraReady?.call();
  }

  Future<Uint8List?> capturePhoto() async {
    final bytes = await _channel?.invokeMethod<Uint8List>('capturePhoto');
    return bytes;
  }

  Future<void> switchCamera() async {
    await _channel?.invokeMethod<void>('switchCamera');
  }

  @override
  Widget build(BuildContext context) {
    const viewType = 'com.example.app/camera-preview';
    final creationParams = <String, dynamic>{
      'facing': widget.facing == CameraFacing.front ? 'front' : 'back',
    };

    if (Platform.isAndroid) {
      return PlatformViewLink(
        viewType: viewType,
        surfaceFactory: (context, controller) {
          return AndroidViewSurface(
            controller: controller as AndroidViewController,
            gestureRecognizers: const <Factory<OneSequenceGestureRecognizer>>{},
            hitTestBehavior: PlatformViewHitTestBehavior.opaque,
          );
        },
        onCreatePlatformView: (params) {
          return PlatformViewsService.initExpensiveAndroidView(
            id: params.id,
            viewType: viewType,
            layoutDirection: TextDirection.ltr,
            creationParams: creationParams,
            creationParamsCodec: const StandardMessageCodec(),
          )
            ..addOnPlatformViewCreatedListener((id) {
              params.onPlatformViewCreated(id);
              _onPlatformViewCreated(id);
            })
            ..create();
        },
      );
    }

    if (Platform.isIOS) {
      return UiKitView(
        viewType: viewType,
        creationParams: creationParams,
        creationParamsCodec: const StandardMessageCodec(),
        onPlatformViewCreated: _onPlatformViewCreated,
      );
    }

    return const Center(child: Text('Camera not supported.'));
  }
}
```

---

## 5. Performance Considerations

### 5.1 Rendering Cost

Each platform view incurs overhead:

| Factor | Impact | Mitigation |
|---|---|---|
| Thread synchronization | 1-3ms per frame per view | Minimize the number of active platform views |
| Texture upload | GPU memory + bandwidth | Keep views at the minimum required resolution |
| Gesture dispatching | Added latency for touch events | Use `gestureRecognizers` to limit forwarded gestures |
| Accessibility bridge | Memory + CPU for a11y tree sync | Ensure native views expose minimal a11y nodes |

### 5.2 Best Practices

1. **Limit active platform views.** Each view creates a separate native surface.
   Aim for one or two per screen maximum.

2. **Use `const` creation params** to avoid unnecessary view recreation.

3. **Dispose views when off-screen.** Use `Visibility` or conditional rendering
   to remove platform views that are not currently visible.

4. **Prefer Texture widget for video.** For simple video playback, use
   `Texture` with a `TextureId` from a platform channel instead of a full
   platform view. This is significantly cheaper.

5. **Profile with DevTools.** Use the Flutter performance overlay and DevTools
   timeline to measure the impact of platform views on frame rendering.

### 5.3 Lazy Initialization Pattern

```dart
import 'dart:io' show Platform;

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

/// Only creates the expensive platform view when it scrolls into the viewport.
class LazyPlatformView extends StatefulWidget {
  const LazyPlatformView({super.key});

  @override
  State<LazyPlatformView> createState() => _LazyPlatformViewState();
}

class _LazyPlatformViewState extends State<LazyPlatformView> {
  bool _isVisible = false;

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      height: 300,
      child: VisibilityDetector(
        key: const Key('lazy-platform-view'),
        onVisibilityChanged: (info) {
          final visible = info.visibleFraction > 0.1;
          if (visible != _isVisible) {
            setState(() => _isVisible = visible);
          }
        },
        child: _isVisible
            ? const NativeMapView(
                initialLatitude: 37.7749,
                initialLongitude: -122.4194,
              )
            : const Center(child: CircularProgressIndicator()),
      ),
    );
  }
}

/// Minimal VisibilityDetector using a LayoutBuilder + scroll notification.
/// In production, use the `visibility_detector` package.
class VisibilityDetector extends StatelessWidget {
  const VisibilityDetector({
    required super.key,
    required this.onVisibilityChanged,
    required this.child,
  });

  final ValueChanged<VisibilityInfo> onVisibilityChanged;
  final Widget child;

  @override
  Widget build(BuildContext context) {
    return LayoutBuilder(
      builder: (context, constraints) {
        // Simplified: always report visible when built.
        WidgetsBinding.instance.addPostFrameCallback((_) {
          onVisibilityChanged(VisibilityInfo(visibleFraction: 1.0));
        });
        return child;
      },
    );
  }
}

class VisibilityInfo {
  const VisibilityInfo({required this.visibleFraction});
  final double visibleFraction;
}
```

---

## 6. Platform-Specific Widget Wrappers

Create a unified API that delegates to the correct platform view:

```dart
import 'dart:io' show Platform;

import 'package:flutter/foundation.dart';
import 'package:flutter/gestures.dart';
import 'package:flutter/material.dart';
import 'package:flutter/rendering.dart';
import 'package:flutter/services.dart';

/// A cross-platform native ad banner.
class NativeAdBanner extends StatelessWidget {
  const NativeAdBanner({
    super.key,
    required this.adUnitId,
    this.width = double.infinity,
    this.height = 50,
  });

  final String adUnitId;
  final double width;
  final double height;

  @override
  Widget build(BuildContext context) {
    const viewType = 'com.example.app/native-ad';
    final params = <String, dynamic>{'adUnitId': adUnitId};

    return SizedBox(
      width: width,
      height: height,
      child: _buildPlatformView(viewType, params),
    );
  }

  Widget _buildPlatformView(String viewType, Map<String, dynamic> params) {
    if (kIsWeb) {
      return const SizedBox.shrink(); // Ads not supported on web.
    }

    if (Platform.isAndroid) {
      return PlatformViewLink(
        viewType: viewType,
        surfaceFactory: (context, controller) {
          return AndroidViewSurface(
            controller: controller as AndroidViewController,
            gestureRecognizers: const <Factory<OneSequenceGestureRecognizer>>{},
            hitTestBehavior: PlatformViewHitTestBehavior.opaque,
          );
        },
        onCreatePlatformView: (viewParams) {
          return PlatformViewsService.initExpensiveAndroidView(
            id: viewParams.id,
            viewType: viewType,
            layoutDirection: TextDirection.ltr,
            creationParams: params,
            creationParamsCodec: const StandardMessageCodec(),
          )
            ..addOnPlatformViewCreatedListener(
              viewParams.onPlatformViewCreated,
            )
            ..create();
        },
      );
    }

    if (Platform.isIOS) {
      return UiKitView(
        viewType: viewType,
        creationParams: params,
        creationParamsCodec: const StandardMessageCodec(),
      );
    }

    return const SizedBox.shrink();
  }
}
```

---

## 7. Platform Detection Patterns

### 7.1 Runtime Platform Checks

```dart
import 'dart:io' show Platform;

import 'package:flutter/foundation.dart' show kIsWeb, defaultTargetPlatform, TargetPlatform;

/// Utility for platform detection that works across all Flutter targets.
abstract final class PlatformInfo {
  /// True when running in a web browser.
  static bool get isWeb => kIsWeb;

  /// True on native iOS (not web pretending to be iOS).
  static bool get isIOS => !kIsWeb && Platform.isIOS;

  /// True on native Android.
  static bool get isAndroid => !kIsWeb && Platform.isAndroid;

  /// True on native macOS desktop.
  static bool get isMacOS => !kIsWeb && Platform.isMacOS;

  /// True on native Windows desktop.
  static bool get isWindows => !kIsWeb && Platform.isWindows;

  /// True on native Linux desktop.
  static bool get isLinux => !kIsWeb && Platform.isLinux;

  /// True on any mobile platform (iOS or Android).
  static bool get isMobile => isIOS || isAndroid;

  /// True on any desktop platform.
  static bool get isDesktop => isMacOS || isWindows || isLinux;

  /// Returns the current platform as a [TargetPlatform] enum.
  /// Safe to call on all platforms including web.
  static TargetPlatform get targetPlatform => defaultTargetPlatform;
}
```

### 7.2 Platform-Adaptive Widget

```dart
import 'package:flutter/cupertino.dart';
import 'package:flutter/material.dart';

/// A button that renders as Material on Android and Cupertino on iOS.
class AdaptiveButton extends StatelessWidget {
  const AdaptiveButton({
    super.key,
    required this.onPressed,
    required this.label,
  });

  final VoidCallback onPressed;
  final String label;

  @override
  Widget build(BuildContext context) {
    if (PlatformInfo.isIOS) {
      return CupertinoButton.filled(
        onPressed: onPressed,
        child: Text(label),
      );
    }
    return FilledButton(
      onPressed: onPressed,
      child: Text(label),
    );
  }
}
```

### 7.3 Conditional Feature Registration

```dart
import 'package:flutter/services.dart';

/// Registers platform-specific features at app startup.
Future<void> registerPlatformFeatures() async {
  if (PlatformInfo.isIOS) {
    // Register iOS-specific handlers (e.g., Handoff, Spotlight).
    const MethodChannel('com.example.app/ios-features')
        .setMethodCallHandler(_handleIOSFeatures);
  }

  if (PlatformInfo.isAndroid) {
    // Register Android-specific handlers (e.g., App Links, shortcuts).
    const MethodChannel('com.example.app/android-features')
        .setMethodCallHandler(_handleAndroidFeatures);
  }
}

Future<dynamic> _handleIOSFeatures(MethodCall call) async {
  switch (call.method) {
    case 'onHandoff':
      // Handle Handoff activity.
      return null;
    default:
      return null;
  }
}

Future<dynamic> _handleAndroidFeatures(MethodCall call) async {
  switch (call.method) {
    case 'onNewIntent':
      // Handle deep link intent.
      return null;
    default:
      return null;
  }
}
```
