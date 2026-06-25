import 'package:flutter_riverpod/flutter_riverpod.dart';

/// The capture-protection seam (`core/secure_surface`, master §4.2). The
/// `PlayerPage` drives the OS black-out through this interface — Android
/// `FLAG_SECURE`, Windows `SetWindowDisplayAffinity(WDA_EXCLUDEFROMCAPTURE)`,
/// macOS `NSWindow.sharingType = .none`, iOS `UIScreen.isCaptured` /
/// `userDidTakeScreenshot` — so the player is unit-tested against a hand fake
/// and `flutter test` **never** constructs a real `MethodChannel`/`EventChannel`
/// (`NFR-APP-REL-003`). Mirrors the `VideoEngine` fakeable-seam shape exactly.
///
/// The production impl is [MethodChannelSecureSurface]; [NoopSecureSurface] is
/// the safe default for any unwired host.
abstract class SecureSurface {
  /// Engages the OS black-out for the player window and reports whether it is
  /// actually guaranteed. Called **before the first frame** (`NFR-APP-CAP-006`).
  /// A platform/version that cannot guarantee the black-out resolves to
  /// [SecureSurfaceStatus.unsupported] — it **never throws** into the player.
  Future<SecureSurfaceStatus> enable();

  /// Releases the black-out on leaving the player (`FR-APP-CAP-003`). Crash-safe
  /// — teardown never throws.
  Future<void> disable();

  /// Reactive capture signals (iOS only — `UIScreen.isCaptured` KVO +
  /// `userDidTakeScreenshot`). An **empty** stream on every other platform, so a
  /// page subscription is a harmless no-op off iOS (`FR-APP-CAP-002`).
  Stream<SecureSurfaceEvent> get captureEvents;
}

/// What the OS secure-surface protection is currently doing for the player.
/// This single enum drives **both** the capture banner (F6) and the COMPAT-002
/// refuse gate (F7), so the on-screen state and the play/refuse decision can
/// never disagree.
enum SecureSurfaceStatus {
  /// The OS black-out is active — screenshots and recordings render black
  /// (`FR-APP-CAP-001`). The happy state → the green reassurance banner.
  protected,

  /// The capability is absent or unguaranteed — Windows < 2004 (build 19041),
  /// or iOS best-effort (recording defeated via `isCaptured`, still-screenshots
  /// an accepted gap, `NFR-APP-CAP-005`). Drives the **amber** banner and, on a
  /// platform where the black-out is *required* (desktop/Android), the
  /// **warn + refuse** path (`NFR-APP-COMPAT-002`).
  unsupported,

  /// Not engaged — outside the player / on error. The banner is hidden.
  off,
}

/// Reactive capture-state signals streamed from the iOS `EventChannel`
/// (`salah_bahzad/secure_surface/events`). Empty/absent on every other
/// platform.
enum SecureSurfaceEvent {
  /// Active screen capture / mirroring **started** → blank/pause playback
  /// (`NFR-APP-CAP-004`, `FR-APP-CAP-002`).
  captureStarted,

  /// Active capture / mirroring **stopped** → resume playback.
  captureStopped,

  /// A still screenshot was taken (`userDidTakeScreenshot`). The **accepted
  /// gap** (`NFR-APP-CAP-005`): log/flag only, **never** block — the visible
  /// watermark is the deterrent.
  screenshotTaken,
}

/// Holds the **real** protection state the player engaged. The page writes it
/// after [SecureSurface.enable] resolves (and resets it to [off] on leave); the
/// view reads it to render the banner variant, and the page reads it to drive
/// the COMPAT-002 refuse gate. A plain app-lifetime [Notifier] (mirrors the
/// `providers.dart` style) — default [SecureSurfaceStatus.off].
class SecureSurfaceStatusController extends Notifier<SecureSurfaceStatus> {
  @override
  SecureSurfaceStatus build() => SecureSurfaceStatus.off;

  void set(SecureSurfaceStatus status) => state = status;
}
