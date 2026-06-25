import 'dart:convert';
import 'dart:io';
import 'dart:typed_data';

import 'package:flutter_test/flutter_test.dart';
import 'package:secure_player/core/net/api_client.dart';
import 'package:secure_player/core/playback/hls_key_loader.dart';
import 'package:secure_player/core/playback/local_manifest_proxy.dart';
import 'package:secure_player/data/playback_repository.dart';

import '../support/playback_fakes.dart';

/// ════════════════════════════════════════════════════════════════════════════
/// F1 SPIKE — the pre-registered A1 technical risk, retired headlessly.
///
/// Risk (master §2 / contract §D): a native HLS engine fetches the
/// `#EXT-X-KEY` URI **without** the app's JWT → the key request `401`s and
/// playback fails. The chosen mitigation is the **engine-agnostic loopback
/// proxy** (`LocalManifestProxy`, reusing the `desktop_oauth_client` 127.0.0.1:0
/// pattern): the engine fetches the key from the in-process proxy, which
/// attaches the Bearer (via the real `ApiClient` → `PlaybackRepository`) and
/// streams the 16 bytes back.
///
/// This walks the EXACT path an HLS engine walks — redeem → load proxied
/// manifest → extract the rewritten key URI → GET the key — against a fake
/// backend that demands the Bearer, using the REAL ApiClient/repository/loader/
/// proxy stack. It proves: (1) the un-augmented native key fetch would 401
/// (negative control); (2) through the proxy the key arrives with the Bearer
/// injected; (3) the key + signed URLs never hit disk, the keystore, or a log.
///
/// What it does NOT prove headlessly: libmpv actually decoding + rendering the
/// decrypted segments (needs native libs + a window). That live decode+render
/// proof is flagged for the A1 wiring stream.
/// ════════════════════════════════════════════════════════════════════════════
void main() {
  late HttpServer backend;
  late String baseUrl;
  late List<String> keyRequestAuthHeaders;
  final Uint8List keyBytes = fixtureKeyBytes();
  const String signedSegmentUrl =
      'https://r2.example.com/seg0.ts?sig=SIGNEDSECRET123';

  setUp(() async {
    keyRequestAuthHeaders = <String>[];
    backend = await HttpServer.bind(InternetAddress.loopbackIPv4, 0);
    baseUrl = 'http://127.0.0.1:${backend.port}';

    backend.listen((HttpRequest req) async {
      final String path = req.uri.path;
      // D2 — redeem: returns a manifest whose key URL points back at THIS
      // backend's D3 endpoint (as the real backend does).
      if (path == '/api/me/videos/playback/redeem' && req.method == 'POST') {
        final String keyUrl = '$baseUrl/api/me/videos/vid-1/hls.key';
        final String manifest = <String>[
          '#EXTM3U',
          '#EXT-X-VERSION:3',
          '#EXT-X-TARGETDURATION:6',
          '#EXT-X-KEY:METHOD=AES-128,URI="$keyUrl",IV=0x00000000000000000000000000000000',
          '#EXTINF:6.0,',
          signedSegmentUrl,
          '#EXT-X-ENDLIST',
        ].join('\n');
        req.response
          ..statusCode = 200
          ..headers.contentType = ContentType.json
          ..write(
            jsonEncode(<String, dynamic>{
              'manifestContent': manifest,
              'keyUrl': keyUrl,
              'expiresAtUtc': DateTime.now()
                  .toUtc()
                  .add(const Duration(seconds: 120))
                  .toIso8601String(),
            }),
          );
        await req.response.close();
        return;
      }
      // D3 — hls.key: REQUIRES the Bearer. Mirrors `RequireStudent`.
      if (path == '/api/me/videos/vid-1/hls.key' && req.method == 'GET') {
        final String? auth = req.headers.value(HttpHeaders.authorizationHeader);
        keyRequestAuthHeaders.add(auth ?? '<none>');
        if (auth == null || !auth.startsWith('Bearer ')) {
          req.response.statusCode = 401;
          await req.response.close();
          return;
        }
        req.response
          ..statusCode = 200
          ..headers.contentType = ContentType('application', 'octet-stream')
          ..add(keyBytes);
        await req.response.close();
        return;
      }
      req.response.statusCode = 404;
      await req.response.close();
    });
  });

  tearDown(() async {
    await backend.close(force: true);
  });

  test(
    'authenticated AES-128 key fetch works end-to-end through the loopback '
    'proxy (F1 risk retired)',
    () async {
      // ── Negative control: the un-augmented native fetch 401s ──────────────
      final HttpClient bare = HttpClient();
      final HttpClientRequest unauthReq = await bare.getUrl(
        Uri.parse('$baseUrl/api/me/videos/vid-1/hls.key'),
      );
      final HttpClientResponse unauthRes = await unauthReq.close();
      expect(
        unauthRes.statusCode,
        401,
        reason: 'A key fetch without the Bearer must 401 — the exact risk.',
      );
      bare.close(force: true);

      // ── The real app stack ────────────────────────────────────────────────
      final FakeSessionStore store = FakeSessionStore(
        session: validSession('access-jwt-123'),
      );
      final ApiClient client = realApiClient(baseUrl: baseUrl, store: store);
      addTearDown(client.dispose);
      final PlaybackRepository repo = PlaybackRepository(client);
      final HlsKeyLoader loader = HlsKeyLoader(repo);

      final CapturingLogSink sink = CapturingLogSink();
      final LocalManifestProxy proxy = LocalManifestProxy(
        loader,
        logger: capturingLogger(sink),
      );
      addTearDown(proxy.stop);

      // 1) Redeem (D2) → manifest. No view spent.
      final manifest = await repo.redeem('deadbeef-handoff');
      expect(manifest.keyUrl, contains('/hls.key'));

      // 2) Start the proxy and get the loopback URL the engine would open.
      final Uri localUrl = await proxy.start(
        manifest: manifest,
        videoId: 'vid-1',
      );
      expect(localUrl.host, '127.0.0.1');

      // 3) Engine step: load the proxied manifest, confirm the key URI now
      //    points at the proxy (NOT the backend), and the signed segment URL is
      //    untouched.
      final HttpClient engine = HttpClient();
      addTearDown(() => engine.close(force: true));
      final HttpClientResponse manifestRes =
          await (await engine.getUrl(localUrl)).close();
      final String servedManifest =
          await manifestRes.transform(utf8.decoder).join();
      expect(manifestRes.statusCode, 200);
      expect(
        servedManifest,
        contains('URI="http://127.0.0.1:'),
        reason: 'The key URI must be rewritten to the loopback proxy.',
      );
      expect(
        servedManifest,
        isNot(contains('$baseUrl/api/me/videos/vid-1/hls.key')),
        reason: 'The backend key URL must not survive in the served manifest.',
      );
      expect(
        servedManifest,
        contains(signedSegmentUrl),
        reason: 'Signed segment URLs are pre-signed and left untouched.',
      );

      // Extract the key URI exactly as an HLS engine would.
      final RegExpMatch? keyLine = RegExp(
        r'URI="([^"]+)"',
      ).firstMatch(servedManifest);
      expect(keyLine, isNotNull);
      final Uri keyUri = Uri.parse(keyLine!.group(1)!);

      // 4) Engine step: GET the key from the proxy (no auth on this hop).
      final HttpClientResponse keyRes =
          await (await engine.getUrl(keyUri)).close();
      expect(keyRes.statusCode, 200);
      final List<int> fetched = <int>[];
      await keyRes.forEach(fetched.addAll);

      // ── Assertions: the key arrived, and the Bearer rode the backend hop ──
      expect(fetched, equals(keyBytes), reason: '16 AES-128 key bytes return.');
      expect(fetched.length, 16);
      expect(
        keyRequestAuthHeaders,
        contains('Bearer access-jwt-123'),
        reason: 'The proxy must authenticate the upstream D3 key fetch.',
      );

      // ── Hygiene (NFR-APP-SEC-005): nothing secret persisted or logged ─────
      // The key bytes never went through the session store (no extra writes).
      expect(
        store.writeCount,
        0,
        reason: 'Neither the key nor any URL is persisted via the keystore.',
      );
      final String hexKey = keyBytes
          .map((int b) => b.toRadixString(16).padLeft(2, '0'))
          .join();
      final String? leak = firstLeak(sink.blob, <String>[
        'access-jwt-123',
        signedSegmentUrl,
        'deadbeef-handoff',
        hexKey,
        String.fromCharCodes(keyBytes),
      ]);
      expect(
        leak,
        isNull,
        reason: 'No token / signed URL / handoff / key bytes may be logged.',
      );
    },
  );
}
