#include "flutter_window.h"

#include <flutter/method_result_functions.h>
#include <flutter/standard_method_codec.h>

#include <memory>
#include <optional>
#include <string>

#include "flutter/generated_plugin_registrant.h"

// WDA_EXCLUDEFROMCAPTURE (the value used for the secure black-out) exists only
// on Windows 10 2004+. Define it defensively in case an older Windows SDK
// header omits it. WDA_NONE (0x0) has been present since Vista.
#ifndef WDA_EXCLUDEFROMCAPTURE
#define WDA_EXCLUDEFROMCAPTURE 0x00000011
#endif
#ifndef WDA_NONE
#define WDA_NONE 0x00000000
#endif

namespace {

// ════════════════════════════════════════════════════════════════════════════
// F1 SPIKE — Windows capture black-out + COMPAT capability gate.
//
// NFR-APP-CAP-002: `SetWindowDisplayAffinity(hwnd, WDA_EXCLUDEFROMCAPTURE)`
// excludes this window from OBS / Snipping Tool / screen recording; `enable`
// applies it and `disable` resets `WDA_NONE`. The *visual* proof (a recorder
// captures black) is the A2 WIRING stream's manual matrix — it can't be
// asserted in `flutter test` (NFR-APP-REL-003).
//
// NFR-APP-COMPAT-001/002: `WDA_EXCLUDEFROMCAPTURE` requires Windows 10 2004 /
// build 19041. On a build < 19041 the black-out can't be GUARANTEED, so we
// report "unsupported" → the Dart facade maps it to
// SecureSurfaceStatus.unsupported → the player WARNS + REFUSES protected
// playback (never plays unprotected).
//
// DECISION (resolves the open question): pre-2004 is **REFUSED**, not silently
// degraded to the legacy `WDA_MONITOR`. `WDA_MONITOR` is NOT a guaranteed
// black-out (master §4.2 specifies only WDA_EXCLUDEFROMCAPTURE / WDA_NONE), and
// COMPAT-002 (master §2) defaults to refuse where the black-out is unguaranteed.
// ════════════════════════════════════════════════════════════════════════════
constexpr DWORD kMinBuildForExcludeFromCapture = 19041;  // Windows 10 2004.

// GetVersionEx is shimmed for unmanifested apps, so query the real build number
// via ntdll's RtlGetVersion. If it can't be determined, fail safe (unsupported).
bool IsCaptureExclusionSupported() {
  using RtlGetVersionPtr = LONG(WINAPI*)(PRTL_OSVERSIONINFOW);
  HMODULE ntdll = ::GetModuleHandleW(L"ntdll.dll");
  if (ntdll != nullptr) {
    auto rtl_get_version = reinterpret_cast<RtlGetVersionPtr>(
        ::GetProcAddress(ntdll, "RtlGetVersion"));
    if (rtl_get_version != nullptr) {
      RTL_OSVERSIONINFOW info = {};
      info.dwOSVersionInfoSize = sizeof(info);
      if (rtl_get_version(&info) == 0 /* STATUS_SUCCESS */) {
        return info.dwBuildNumber >= kMinBuildForExcludeFromCapture;
      }
    }
  }
  return false;
}

}  // namespace

FlutterWindow::FlutterWindow(const flutter::DartProject& project)
    : project_(project) {}

FlutterWindow::~FlutterWindow() {}

bool FlutterWindow::OnCreate() {
  if (!Win32Window::OnCreate()) {
    return false;
  }

  RECT frame = GetClientArea();

  // The size here must match the window dimensions to avoid unnecessary surface
  // creation / destruction in the startup path.
  flutter_controller_ = std::make_unique<flutter::FlutterViewController>(
      frame.right - frame.left, frame.bottom - frame.top, project_);
  // Ensure that basic setup of the controller was successful.
  if (!flutter_controller_->engine() || !flutter_controller_->view()) {
    return false;
  }
  RegisterPlugins(flutter_controller_->engine());

  // A2 capture-protection channel — see the F1 spike note above.
  secure_surface_channel_ =
      std::make_unique<flutter::MethodChannel<flutter::EncodableValue>>(
          flutter_controller_->engine()->messenger(),
          "salah_bahzad/secure_surface",
          &flutter::StandardMethodCodec::GetInstance());
  secure_surface_channel_->SetMethodCallHandler(
      [this](const flutter::MethodCall<flutter::EncodableValue>& call,
             std::unique_ptr<flutter::MethodResult<flutter::EncodableValue>>
                 result) {
        const std::string& method = call.method_name();
        if (method == "isSupported") {
          result->Success(
              flutter::EncodableValue(IsCaptureExclusionSupported()));
        } else if (method == "enable") {
          if (!IsCaptureExclusionSupported()) {
            // COMPAT-002: pre-2004 → refuse (the Dart facade maps this to
            // SecureSurfaceStatus.unsupported and the player won't play).
            result->Success(flutter::EncodableValue("unsupported"));
            return;
          }
          HWND hwnd = GetHandle();
          BOOL ok = hwnd != nullptr &&
                    ::SetWindowDisplayAffinity(hwnd, WDA_EXCLUDEFROMCAPTURE);
          result->Success(
              flutter::EncodableValue(ok ? "protected" : "unsupported"));
        } else if (method == "disable") {
          HWND hwnd = GetHandle();
          if (hwnd != nullptr) {
            ::SetWindowDisplayAffinity(hwnd, WDA_NONE);
          }
          result->Success();
        } else {
          result->NotImplemented();
        }
      });

  SetChildContent(flutter_controller_->view()->GetNativeWindow());

  flutter_controller_->engine()->SetNextFrameCallback([&]() {
    this->Show();
  });

  // Flutter can complete the first frame before the "show window" callback is
  // registered. The following call ensures a frame is pending to ensure the
  // window is shown. It is a no-op if the first frame hasn't completed yet.
  flutter_controller_->ForceRedraw();

  return true;
}

void FlutterWindow::OnDestroy() {
  // Tear down the channel before the engine/messenger it binds to.
  secure_surface_channel_ = nullptr;
  if (flutter_controller_) {
    flutter_controller_ = nullptr;
  }

  Win32Window::OnDestroy();
}

LRESULT
FlutterWindow::MessageHandler(HWND hwnd, UINT const message,
                              WPARAM const wparam,
                              LPARAM const lparam) noexcept {
  // Give Flutter, including plugins, an opportunity to handle window messages.
  if (flutter_controller_) {
    std::optional<LRESULT> result =
        flutter_controller_->HandleTopLevelWindowProc(hwnd, message, wparam,
                                                      lparam);
    if (result) {
      return *result;
    }
  }

  switch (message) {
    case WM_FONTCHANGE:
      flutter_controller_->engine()->ReloadSystemFonts();
      break;
  }

  return Win32Window::MessageHandler(hwnd, message, wparam, lparam);
}
