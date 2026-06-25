import Flutter
import UIKit

@main
@objc class AppDelegate: FlutterAppDelegate, FlutterImplicitEngineDelegate {
  override func application(
    _ application: UIApplication,
    didFinishLaunchingWithOptions launchOptions: [UIApplication.LaunchOptionsKey: Any]?
  ) -> Bool {
    return super.application(application, didFinishLaunchingWithOptions: launchOptions)
  }

  func didInitializeImplicitFlutterEngine(_ engineBridge: FlutterImplicitEngineBridge) {
    GeneratedPluginRegistrant.register(with: engineBridge.pluginRegistry)

    // A2 capture protection (NFR-APP-CAP-004/005) — WRITTEN-NOT-BUILT-HERE.
    // Authored on a Windows dev box (no Swift/Xcode toolchain) — build &
    // capture-verify in a Mac session via `flutter build ios`.
    if let registrar = engineBridge.pluginRegistry.registrar(forPlugin: "SecureSurfacePlugin") {
      SecureSurfacePlugin.register(messenger: registrar.messenger())
    }
  }
}

/// iOS capture protection over the `salah_bahzad/secure_surface` MethodChannel
/// (enable/disable/isSupported) plus the `…/events` EventChannel.
///
/// iOS is the one **best-effort** target:
/// * Active screen capture / mirroring is detected via `UIScreen.isCaptured`
///   (KVO) → the app blanks/pauses (NFR-APP-CAP-004, FR-APP-CAP-002), streamed
///   as `capture_started` / `capture_stopped`.
/// * A single still screenshot **cannot** be blocked without FairPlay — the
///   accepted gap (NFR-APP-CAP-005): `userDidTakeScreenshot` is streamed as
///   `screenshot` to log/flag only, and the visible watermark is the deterrent.
///
/// Because the still-screenshot black-out can't be guaranteed, `enable` reports
/// `"unsupported"` — but iOS is exempt from the COMPAT-002 desktop refuse path,
/// so the player still plays (best-effort) with the amber banner + watermark.
///
/// WRITTEN-NOT-BUILT-HERE — not compiled on this Windows box.
final class SecureSurfacePlugin: NSObject, FlutterStreamHandler {
  private var eventSink: FlutterEventSink?
  private var captureObservation: NSKeyValueObservation?

  static func register(messenger: FlutterBinaryMessenger) {
    let instance = SecureSurfacePlugin()

    let methodChannel = FlutterMethodChannel(
      name: "salah_bahzad/secure_surface", binaryMessenger: messenger)
    methodChannel.setMethodCallHandler { call, result in
      switch call.method {
      case "isSupported":
        // Best-effort: recording defeated, still-screenshot an accepted gap.
        result("unsupported")
      case "enable":
        result("unsupported")
      case "disable":
        result(nil)
      default:
        result(FlutterMethodNotImplemented)
      }
    }

    let eventChannel = FlutterEventChannel(
      name: "salah_bahzad/secure_surface/events", binaryMessenger: messenger)
    eventChannel.setStreamHandler(instance)
  }

  func onListen(
    withArguments arguments: Any?, eventSink events: @escaping FlutterEventSink
  ) -> FlutterError? {
    self.eventSink = events
    // Emit the current capture state immediately, then observe changes (KVO).
    emitCapture(UIScreen.main.isCaptured)
    captureObservation = UIScreen.main.observe(\.isCaptured, options: [.new]) {
      [weak self] screen, _ in
      self?.emitCapture(screen.isCaptured)
    }
    NotificationCenter.default.addObserver(
      self, selector: #selector(onScreenshot),
      name: UIApplication.userDidTakeScreenshotNotification, object: nil)
    return nil
  }

  func onCancel(withArguments arguments: Any?) -> FlutterError? {
    captureObservation?.invalidate()
    captureObservation = nil
    NotificationCenter.default.removeObserver(self)
    eventSink = nil
    return nil
  }

  private func emitCapture(_ active: Bool) {
    eventSink?(active ? "capture_started" : "capture_stopped")
  }

  @objc private func onScreenshot() {
    // Accepted gap (NFR-APP-CAP-005): flag only, never block.
    eventSink?("screenshot")
  }
}
