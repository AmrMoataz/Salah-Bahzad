import 'dart:async';
import 'dart:io';
import 'dart:typed_data';

import '../../data/dtos/playback_manifest.dart';
import '../logging/logging.dart';
import 'hls_key_loader.dart';

/// The **engine-agnostic** mechanism that lets a native HLS engine play an
/// AES-128 stream whose key needs the app's JWT (the pre-registered A1 risk).
///
/// It runs a Dart [HttpServer] bound to `127.0.0.1:0` (the same loopback
/// precedent as `desktop_oauth_client.dart`) and serves, **from memory only**:
///
///  * `…/manifest.m3u8` — the redeemed [PlaybackManifest.manifestContent] with
///    its `#EXT-X-KEY URI="…"` rewritten to point back at this proxy's `/key`
///    endpoint (the signed R2 segment URLs are left untouched — they are
///    pre-signed and need no Bearer);
///  * `…/key` — fetches the 16 AES-128 key bytes through [HlsKeyLoader] (which
///    authenticates with the Bearer) and streams them to the engine.
///
/// Nothing here touches disk: the manifest lives in a field, the key bytes are
/// fetched per request and never cached. Bound to loopback only, so no other
/// process can reach it. Lives only while the player is mounted ([start]/[stop])
/// (`NFR-APP-SEC-005`).
class LocalManifestProxy {
  LocalManifestProxy(this._keyLoader, {AppLogger? logger})
    : _log = logger ?? Log.scoped('playback');

  final HlsKeyLoader _keyLoader;
  final AppLogger _log;

  static const String _manifestPath = '/manifest.m3u8';
  static const String _keyPath = '/key';

  HttpServer? _server;
  StreamSubscription<HttpRequest>? _sub;

  /// The current playback context, held **in memory only**.
  PlaybackManifest? _manifest;
  String? _videoId;

  bool get isRunning => _server != null;

  /// Starts the proxy for [manifest] (the redeemed manifest) + [videoId] (the
  /// video whose key the engine will request). Returns the loopback URL the
  /// engine should open. Idempotent context-wise: calling [start] again swaps
  /// the served context on the same socket (used by an in-TTL retry, so the
  /// engine can re-open without re-redeeming — no double-decrement).
  Future<Uri> start({
    required PlaybackManifest manifest,
    required String videoId,
  }) async {
    _manifest = manifest;
    _videoId = videoId;
    if (_server == null) {
      final HttpServer server = await HttpServer.bind(
        InternetAddress.loopbackIPv4,
        0,
      );
      _server = server;
      _sub = server.listen(
        _handle,
        onError: (Object e, StackTrace s) =>
            _log.warning('Manifest proxy socket error', error: e),
      );
      _log.info('Local manifest proxy started');
    }
    return Uri.parse('http://127.0.0.1:${_server!.port}$_manifestPath');
  }

  /// Tears the proxy down and drops the in-memory manifest/key context.
  Future<void> stop() async {
    _manifest = null;
    _videoId = null;
    await _sub?.cancel();
    _sub = null;
    final HttpServer? server = _server;
    _server = null;
    if (server != null) {
      await server.close(force: true);
      _log.info('Local manifest proxy stopped');
    }
  }

  Future<void> _handle(HttpRequest request) async {
    final String path = request.uri.path;
    try {
      switch (path) {
        case _manifestPath:
          await _serveManifest(request);
        case _keyPath:
          await _serveKey(request);
        default:
          request.response.statusCode = HttpStatus.notFound;
          await request.response.close();
      }
    } catch (e, s) {
      // Never let a proxy hiccup throw on the socket; the engine will surface a
      // load error which the controller maps to a ret[r]yable state.
      _log.warning('Manifest proxy request failed', error: e, stackTrace: s);
      try {
        request.response.statusCode = HttpStatus.badGateway;
        await request.response.close();
      } catch (_) {
        // Response already (partly) sent — nothing more to do.
      }
    }
  }

  Future<void> _serveManifest(HttpRequest request) async {
    final PlaybackManifest? manifest = _manifest;
    if (manifest == null) {
      request.response.statusCode = HttpStatus.notFound;
      await request.response.close();
      return;
    }
    final Uri keyUri = Uri.parse(
      'http://127.0.0.1:${_server!.port}$_keyPath',
    );
    final String body = rewriteKeyUri(
      manifest.manifestContent,
      originalKeyUrl: manifest.keyUrl,
      proxyKeyUri: keyUri,
    );
    request.response
      ..statusCode = HttpStatus.ok
      ..headers.contentType = ContentType(
        'application',
        'vnd.apple.mpegurl',
      )
      ..write(body);
    await request.response.close();
  }

  Future<void> _serveKey(HttpRequest request) async {
    final String? videoId = _videoId;
    if (videoId == null) {
      request.response.statusCode = HttpStatus.notFound;
      await request.response.close();
      return;
    }
    // Fetch through the authenticated loader; the bytes are never cached here.
    final Uint8List key = await _keyLoader.load(videoId);
    request.response
      ..statusCode = HttpStatus.ok
      ..headers.contentType = ContentType('application', 'octet-stream')
      ..headers.set(HttpHeaders.contentLengthHeader, key.length)
      ..add(key);
    await request.response.close();
  }

  /// Rewrites the `#EXT-X-KEY` key URI so the engine fetches the key from this
  /// proxy instead of the backend. Signed segment URLs are left as-is.
  ///
  /// Replaces the known absolute [originalKeyUrl] when present; otherwise falls
  /// back to rewriting any `URI="…"` attribute on an `#EXT-X-KEY` line. Pure +
  /// unit-tested.
  static String rewriteKeyUri(
    String manifest, {
    required String originalKeyUrl,
    required Uri proxyKeyUri,
  }) {
    final String proxy = proxyKeyUri.toString();
    if (originalKeyUrl.isNotEmpty && manifest.contains(originalKeyUrl)) {
      return manifest.replaceAll(originalKeyUrl, proxy);
    }
    final RegExp keyLine = RegExp(
      r'(#EXT-X-KEY:[^\r\n]*?URI=")([^"]*)(")',
      caseSensitive: false,
    );
    return manifest.replaceAllMapped(
      keyLine,
      (Match m) => '${m.group(1)}$proxy${m.group(3)}',
    );
  }
}
