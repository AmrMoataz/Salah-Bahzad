package com.salahbahzad.secure_player

import android.view.WindowManager
import io.flutter.embedding.android.FlutterActivity
import io.flutter.embedding.engine.FlutterEngine
import io.flutter.plugin.common.MethodChannel

/**
 * Hosts the A2 capture-protection channel (`salah_bahzad/secure_surface`).
 *
 * `enable` sets `WindowManager.LayoutParams.FLAG_SECURE` on the activity window
 * so screenshots and screen recording render **black** and the app is excluded
 * from the recent-apps thumbnail (NFR-APP-CAP-001); `disable` clears it.
 * `FLAG_SECURE` predates every supported OS floor (API 1), so Android is always
 * supported at our `minSdk = 23` — `isSupported` is unconditionally `true`. No
 * DRM is involved.
 *
 * Builds + capture-verifies on this Windows dev box (via `flutter build apk`).
 */
class MainActivity : FlutterActivity() {
    private val channelName = "salah_bahzad/secure_surface"

    override fun configureFlutterEngine(flutterEngine: FlutterEngine) {
        super.configureFlutterEngine(flutterEngine)
        MethodChannel(flutterEngine.dartExecutor.binaryMessenger, channelName)
            .setMethodCallHandler { call, result ->
                when (call.method) {
                    "isSupported" -> result.success(true)
                    "enable" -> {
                        window.addFlags(WindowManager.LayoutParams.FLAG_SECURE)
                        result.success("protected")
                    }
                    "disable" -> {
                        window.clearFlags(WindowManager.LayoutParams.FLAG_SECURE)
                        result.success(null)
                    }
                    else -> result.notImplemented()
                }
            }
    }
}
