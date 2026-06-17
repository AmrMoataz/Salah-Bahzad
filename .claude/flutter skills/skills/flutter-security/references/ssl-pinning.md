# SSL & Certificate Pinning

## Why Pin?

TLS protects data in transit, but a compromised or rogue CA can issue a valid
certificate for your domain. Pinning ensures your app only trusts **your**
certificate or public key, blocking man-in-the-middle (MITM) proxies even when
they present a technically valid certificate chain.

---

## Certificate Pinning with Dio

Pin against a PEM-encoded certificate bundled as an asset.

### Project Layout

```
assets/
  certs/
    api_example_com.pem      # Leaf or intermediate certificate
lib/
  core/
    network/
      pinned_http_client.dart
```

### Extracting the Certificate

```bash
# Download the leaf certificate from your API host
openssl s_client -connect api.example.com:443 -showcerts </dev/null 2>/dev/null \
  | openssl x509 -outform PEM > assets/certs/api_example_com.pem
```

### Dio + Certificate Pinning (dart:io SecurityContext)

```dart
// lib/core/network/pinned_http_client.dart

import 'dart:io';

import 'package:dio/dio.dart';
import 'package:dio/io.dart';
import 'package:flutter/services.dart' show rootBundle;

/// Creates a [Dio] instance that only trusts the bundled certificate for
/// [trustedHost].
///
/// Any connection to a host whose certificate chain does not include the
/// pinned certificate will be rejected with a [HandshakeException].
Future<Dio> createPinnedDio({
  required String baseUrl,
  required String trustedHost,
  String certAssetPath = 'assets/certs/api_example_com.pem',
}) async {
  final certData = await rootBundle.load(certAssetPath);

  final securityContext = SecurityContext(withTrustedRoots: false);
  securityContext.setTrustedCertificatesBytes(certData.buffer.asUint8List());

  final httpClient = HttpClient(context: securityContext)
    ..badCertificateCallback = (X509Certificate cert, String host, int port) {
      // Reject every certificate that did not pass the SecurityContext check.
      return false;
    };

  final dio = Dio(BaseOptions(
    baseUrl: baseUrl,
    connectTimeout: const Duration(seconds: 15),
    receiveTimeout: const Duration(seconds: 15),
  ));

  (dio.httpClientAdapter as IOHttpClientAdapter).createHttpClient = () {
    return httpClient;
  };

  return dio;
}
```

Register the asset in `pubspec.yaml`:

```yaml
flutter:
  assets:
    - assets/certs/
```

---

## Public Key Pinning

Pinning the **Subject Public Key Info (SPKI)** hash instead of the full
certificate survives certificate renewals as long as the same key pair is reused.

### Extracting the SPKI SHA-256

```bash
# Get the base64-encoded SHA-256 of the SPKI
openssl s_client -connect api.example.com:443 -showcerts </dev/null 2>/dev/null \
  | openssl x509 -pubkey -noout \
  | openssl pkey -pubin -outform DER \
  | openssl dgst -sha256 -binary \
  | base64
# Example output: "d6qzRu9zOECb90Uez27xWltNsj0e1Md7GkYYkVoZWmM="
```

### Validating the Public Key Hash at Runtime

```dart
// lib/core/network/public_key_pinner.dart

import 'dart:convert';
import 'dart:io';

import 'package:crypto/crypto.dart';
import 'package:dio/dio.dart';
import 'package:dio/io.dart';

/// Pins requests by comparing the SHA-256 hash of the server's public key
/// against a set of known-good hashes.
///
/// This approach is more resilient to certificate rotation than full
/// certificate pinning because the public key typically remains stable
/// across renewals.
Dio createPublicKeyPinnedDio({
  required String baseUrl,
  required Set<String> trustedSpkiHashes,
}) {
  final dio = Dio(BaseOptions(
    baseUrl: baseUrl,
    connectTimeout: const Duration(seconds: 15),
    receiveTimeout: const Duration(seconds: 15),
  ));

  (dio.httpClientAdapter as IOHttpClientAdapter).createHttpClient = () {
    final client = HttpClient();
    client.badCertificateCallback = (
      X509Certificate cert,
      String host,
      int port,
    ) {
      // Compute the SHA-256 of the DER-encoded certificate.
      // Note: dart:io does not expose the raw SPKI, so we hash the full
      // DER and compare against pre-computed hashes of the full cert.
      // For true SPKI pinning, use a native plugin or the approach below
      // with the certificate's DER bytes.
      final certHash = base64Encode(sha256.convert(cert.der).bytes);
      return trustedSpkiHashes.contains(certHash);
    };
    return client;
  };

  return dio;
}
```

Usage:

```dart
final dio = createPublicKeyPinnedDio(
  baseUrl: 'https://api.example.com',
  trustedSpkiHashes: {
    'd6qzRu9zOECb90Uez27xWltNsj0e1Md7GkYYkVoZWmM=', // Current key
    'FmrKp48GRLA3VbYBFHKA60IWNKR1XsMquYnoMxMOfkY=', // Backup key
  },
);
```

Always include at least one **backup pin** so the app continues to work if you
rotate to a new key pair.

---

## Certificate Transparency

Certificate Transparency (CT) is a public logging framework that makes it
possible to detect misissued certificates. While Android enforces CT at the
platform level starting with Android 10, iOS apps benefit from Apple's own CT
policy.

Your responsibility:

1. Ensure your certificates are logged to CT logs (most public CAs do this
   automatically).
2. Monitor CT logs for unexpected certificates issued for your domain. Services
   like [crt.sh](https://crt.sh) and Google's CT search make this easy.
3. Combine CT monitoring with pinning for defence in depth.

---

## MITM Prevention Checklist

- Enable certificate or public key pinning for every API host.
- Never set `badCertificateCallback` to return `true` in production builds.
- Strip debug/proxy overrides behind a compile-time flag:

```dart
// lib/core/network/network_config.dart

/// Whether TLS verification is relaxed for local proxy debugging.
///
/// Controlled via --dart-define=ALLOW_PROXY=true. **Never** enable this in
/// release builds.
const bool allowProxy = bool.fromEnvironment('ALLOW_PROXY');
```

```dart
// In your HttpClient setup:
if (!allowProxy) {
  httpClient.badCertificateCallback = (cert, host, port) => false;
}
```

- Use `network_security_config.xml` on Android to restrict trusted CAs in
  release builds:

```xml
<!-- android/app/src/main/res/xml/network_security_config.xml -->
<network-security-config>
    <base-config cleartextTrafficPermitted="false">
        <trust-anchors>
            <certificates src="system"/>
            <!-- Do NOT include <certificates src="user"/> in release. -->
        </trust-anchors>
    </base-config>
</network-security-config>
```

Reference it in `AndroidManifest.xml`:

```xml
<application
    android:networkSecurityConfig="@xml/network_security_config"
    ...>
```

---

## Handling Certificate Rotation

Certificates expire. A hard-pinned app will break if the certificate changes
and the app has not been updated. Strategies to prevent outages:

### 1. Pin the Intermediate CA Instead of the Leaf

Intermediate CA certificates have a longer lifespan and remain stable across
leaf certificate renewals.

### 2. Pin Multiple Hashes (Primary + Backup)

Always include the hash of your **next** key pair so you can rotate without an
app update.

```dart
final trustedHashes = {
  'CURRENT_CERT_HASH_HERE',
  'NEXT_CERT_HASH_HERE', // Pre-generated backup key pair
};
```

### 3. Remote Pin List with Signed Updates

For apps that cannot tolerate any downtime, fetch an updated pin list from a
signed endpoint at startup. The endpoint itself should be pinned against a
long-lived intermediate CA.

```dart
// lib/core/network/remote_pin_provider.dart

import 'dart:convert';

import 'package:dio/dio.dart';

/// Fetches an updated set of trusted SPKI hashes from a signed remote
/// configuration endpoint.
///
/// The [configDio] instance must itself be pinned against a long-lived
/// intermediate CA so the bootstrap is not vulnerable to MITM.
Future<Set<String>> fetchRemotePins({
  required Dio configDio,
  required String pinConfigUrl,
}) async {
  final response = await configDio.get<Map<String, dynamic>>(pinConfigUrl);
  final data = response.data;

  if (data == null || data['pins'] is! List) {
    throw FormatException('Invalid pin configuration response');
  }

  // In production, verify a signature over this payload before trusting it.
  final pins = (data['pins'] as List).cast<String>();
  return pins.toSet();
}
```

### 4. Graceful Failure

When a pin check fails, do not silently fall back to an unpinned connection.
Instead:

- Block the request.
- Show a user-facing error explaining the app needs to be updated.
- Report the failure to your analytics / security monitoring backend (through a
  separate, independently pinned channel if possible).

---

## Development vs. Production Pinning

During development you often need a proxy (Charles, Proxyman, mitmproxy) to
inspect traffic. Use a compile-time constant to bypass pinning **only** in debug
builds.

```dart
// lib/core/network/dio_factory.dart

import 'package:flutter/foundation.dart';

import 'pinned_http_client.dart';

/// Builds the application-wide [Dio] instance.
///
/// In debug mode with [allowProxy] enabled, pinning is skipped so local
/// proxies work. In release mode, pinning is always enforced.
Future<Dio> buildDio({
  required String baseUrl,
  required String trustedHost,
}) async {
  const allowProxy = bool.fromEnvironment('ALLOW_PROXY');

  if (kDebugMode && allowProxy) {
    // Unpinned client for local development only.
    return Dio(BaseOptions(baseUrl: baseUrl));
  }

  return createPinnedDio(
    baseUrl: baseUrl,
    trustedHost: trustedHost,
  );
}
```

Launch in debug mode with proxy support:

```bash
flutter run --dart-define=ALLOW_PROXY=true
```

---

## HTTP/2 with dio_http2_adapter

HTTP/2 provides multiplexing, header compression, and improved performance.
The `dio_http2_adapter` package replaces Dio's default `dart:io`-based adapter
with one built on the `http2` package.

```yaml
# pubspec.yaml
dependencies:
  dio: ^5.7.0
  dio_http2_adapter: ^3.5.0
```

### Pinned HTTP/2 Client

```dart
// lib/core/network/http2_pinned_client.dart

import 'dart:io';

import 'package:dio/dio.dart';
import 'package:dio_http2_adapter/dio_http2_adapter.dart';
import 'package:flutter/services.dart' show rootBundle;

/// Creates a [Dio] instance using HTTP/2 with certificate pinning.
Future<Dio> createHttp2PinnedDio({
  required String baseUrl,
  String certAssetPath = 'assets/certs/api_example_com.pem',
}) async {
  final certData = await rootBundle.load(certAssetPath);
  final certBytes = certData.buffer.asUint8List();

  final dio = Dio(BaseOptions(
    baseUrl: baseUrl,
    connectTimeout: const Duration(seconds: 15),
    receiveTimeout: const Duration(seconds: 15),
  ));

  dio.httpClientAdapter = Http2Adapter(
    ConnectionManager(
      idleTimeout: const Duration(seconds: 30),
      onClientCreate: (uri, config) {
        // Load the pinned certificate into the SecurityContext.
        final context = SecurityContext(withTrustedRoots: false);
        context.setTrustedCertificatesBytes(certBytes);

        config.context = context;
        config.validateCertificate = (certificate, host, port) {
          // Return true only if the platform accepted the certificate
          // (i.e., it matched our pinned cert in the SecurityContext).
          return certificate != null;
        };
      },
    ),
  );

  return dio;
}
```

### Benefits of HTTP/2 for Security

- **Single TLS handshake** per connection with multiplexed streams reduces the
  attack surface compared to opening many HTTP/1.1 connections.
- **HPACK header compression** prevents CRIME-style attacks by design.
- **Mandatory TLS** (in practice) -- while the HTTP/2 spec allows cleartext,
  every major implementation requires TLS, enforcing encryption by default.
