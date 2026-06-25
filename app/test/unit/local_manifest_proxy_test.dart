import 'dart:convert';
import 'dart:io';

import 'package:flutter_test/flutter_test.dart';
import 'package:secure_player/core/playback/hls_key_loader.dart';
import 'package:secure_player/core/playback/local_manifest_proxy.dart';

import '../support/playback_fakes.dart';

void main() {
  group('rewriteKeyUri (pure)', () {
    final Uri proxyKey = Uri.parse('http://127.0.0.1:5555/key');

    test('replaces the known key URL, leaves signed segment URLs', () {
      const String original = 'https://api.test/api/me/videos/v/hls.key';
      final String manifest = <String>[
        '#EXTM3U',
        '#EXT-X-KEY:METHOD=AES-128,URI="$original",IV=0x1',
        '#EXTINF:6.0,',
        'https://r2.test/seg0.ts?sig=KEEPME',
      ].join('\n');

      final String out = LocalManifestProxy.rewriteKeyUri(
        manifest,
        originalKeyUrl: original,
        proxyKeyUri: proxyKey,
      );

      expect(out, contains('URI="http://127.0.0.1:5555/key"'));
      expect(out, isNot(contains(original)));
      expect(out, contains('https://r2.test/seg0.ts?sig=KEEPME'));
    });

    test('falls back to the URI attribute when the literal URL is unknown', () {
      final String manifest = <String>[
        '#EXTM3U',
        '#EXT-X-KEY:METHOD=AES-128,URI="https://elsewhere/k",IV=0x2',
        'https://r2.test/seg.ts',
      ].join('\n');

      final String out = LocalManifestProxy.rewriteKeyUri(
        manifest,
        originalKeyUrl: '', // unknown
        proxyKeyUri: proxyKey,
      );
      expect(out, contains('URI="http://127.0.0.1:5555/key"'));
      expect(out, isNot(contains('https://elsewhere/k')));
    });
  });

  group('proxy serves a rewritten manifest + authenticated key', () {
    late LocalManifestProxy proxy;
    late FakePlaybackRepository repo;

    setUp(() {
      repo = FakePlaybackRepository();
      proxy = LocalManifestProxy(HlsKeyLoader(repo));
    });

    tearDown(() => proxy.stop());

    test('manifest key URI points at the proxy; key returns 16 bytes', () async {
      final Uri url = await proxy.start(
        manifest: fixtureManifest(),
        videoId: 'vid-1',
      );

      final HttpClient http = HttpClient();
      addTearDown(() => http.close(force: true));

      final HttpClientResponse mres = await (await http.getUrl(url)).close();
      final String manifest = await mres.transform(utf8.decoder).join();
      expect(manifest, contains('URI="http://127.0.0.1:'));
      expect(manifest, contains('seg0.ts?sig=SIGNED')); // untouched

      final Uri keyUri = Uri.parse(
        RegExp(r'URI="([^"]+)"').firstMatch(manifest)!.group(1)!,
      );
      final HttpClientResponse kres = await (await http.getUrl(keyUri)).close();
      expect(kres.statusCode, 200);
      final List<int> bytes = <int>[];
      await kres.forEach(bytes.addAll);
      expect(bytes, equals(fixtureKeyBytes()));
      expect(repo.keyCalls, 1, reason: 'key fetched through the loader');
    });

    test('after stop, the in-memory manifest/key context is dropped', () async {
      final Uri url = await proxy.start(
        manifest: fixtureManifest(),
        videoId: 'vid-1',
      );
      await proxy.stop();
      expect(proxy.isRunning, isFalse);
      // The socket is closed — a fetch now fails to connect.
      final HttpClient http = HttpClient();
      addTearDown(() => http.close(force: true));
      await expectLater(
        () async => (await http.getUrl(url)).close(),
        throwsA(isA<SocketException>()),
      );
    });
  });
}
