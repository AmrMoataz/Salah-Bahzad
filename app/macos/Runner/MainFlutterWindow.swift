import Cocoa
import FlutterMacOS

class MainFlutterWindow: NSWindow {
  override func awakeFromNib() {
    let flutterViewController = FlutterViewController()
    let windowFrame = self.frame
    self.contentViewController = flutterViewController
    self.setFrame(windowFrame, display: true)

    RegisterGeneratedPlugins(registry: flutterViewController)

    // ─────────────────────────────────────────────────────────────────────────
    // A2 capture protection (NFR-APP-CAP-003) — WRITTEN-NOT-BUILT-HERE.
    // This is a Windows dev box with no Swift/Xcode toolchain, so this shim is
    // authored but NOT compiled or capture-verified here (do that in a Mac
    // session via `flutter build macos`).
    //
    // `enable`  → NSWindow.sharingType = .none  (excludes the window from screen
    //             sharing / recording / window screenshots; no DRM).
    // `disable` → restore .readOnly  (the non-protected default — NOT .none).
    // `isSupported` → true (sharingType predates the macOS 11 floor).
    // ─────────────────────────────────────────────────────────────────────────
    let secureChannel = FlutterMethodChannel(
      name: "salah_bahzad/secure_surface",
      binaryMessenger: flutterViewController.engine.binaryMessenger)
    secureChannel.setMethodCallHandler { [weak self] call, result in
      switch call.method {
      case "isSupported":
        result(true)
      case "enable":
        guard let window = self else {
          result("unsupported")
          return
        }
        window.sharingType = .none
        result("protected")
      case "disable":
        self?.sharingType = .readOnly
        result(nil)
      default:
        result(FlutterMethodNotImplemented)
      }
    }

    super.awakeFromNib()
  }
}
