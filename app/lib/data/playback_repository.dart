import 'dart:typed_data';

import 'package:dio/dio.dart';

import '../core/net/api_client.dart';
import '../core/net/api_exception.dart';
import 'dtos/playback_handoff.dart';
import 'dtos/playback_manifest.dart';

/// The secure-video gate (contract §D) over the existing [ApiClient] — so every
/// call inherits the Bearer injection, the `X-App-Version`/`X-Platform` headers
/// (§G), and the single-flight 401 refresh. The three routes the app drives:
///
///  * [redeem] (D2) — handoff → manifest. **No view spent.**
///  * [startPlayback] (D1) — spends one view, mints a handoff. The deep-link
///    path does **not** use this (the portal already minted the handoff); it
///    exists only for a direct-play path.
///  * [keyBytes] (D3) — the raw 16 AES-128 key bytes for the key-loader.
///
/// Gate errors (§D.2) surface as a typed [ApiException] carrying the HTTP status
/// + `reason` + `detail`, which the player maps to a state (§H). The `426
/// outdated_app` reason is **A4**, not handled here.
class PlaybackRepository {
  PlaybackRepository(this._client);

  final ApiClient _client;

  /// D2 — `POST /api/me/videos/playback/redeem` body `{ "handoffCode": "…" }`.
  /// Returns the rewritten manifest; the server enforces `handoff.StudentId ==
  /// caller`. **No view is spent** (the view was spent by D1 upstream).
  Future<PlaybackManifest> redeem(String handoffCode) async {
    try {
      final Response<dynamic> res = await _client.dio.post<dynamic>(
        '/api/me/videos/playback/redeem',
        data: <String, dynamic>{'handoffCode': handoffCode},
      );
      return PlaybackManifest.fromJson(
        (res.data as Map).cast<String, dynamic>(),
      );
    } on DioException catch (e) {
      throw ApiException.fromDio(e);
    }
  }

  /// D1 — `POST /api/me/videos/{videoId}/playback` (no body). **Spends one
  /// view** and mints a ~60 s handoff (audits `VideoPlaybackStarted`). Only for
  /// a direct-play path — the deep-link player flow is D2 → D3, never D1.
  Future<PlaybackHandoff> startPlayback(String videoId) async {
    try {
      final Response<dynamic> res = await _client.dio.post<dynamic>(
        '/api/me/videos/$videoId/playback',
      );
      return PlaybackHandoff.fromJson(
        (res.data as Map).cast<String, dynamic>(),
      );
    } on DioException catch (e) {
      throw ApiException.fromDio(e);
    }
  }

  /// D3 — `GET /api/me/videos/{videoId}/hls.key` → the raw 16 AES-128 key bytes
  /// (`application/octet-stream`). The caller keeps the returned [Uint8List]
  /// **in memory only** and never persists or logs it (`NFR-APP-SEC-005`).
  Future<Uint8List> keyBytes(String videoId) async {
    try {
      final Response<List<int>> res = await _client.dio.get<List<int>>(
        '/api/me/videos/$videoId/hls.key',
        options: Options(responseType: ResponseType.bytes),
      );
      return Uint8List.fromList(res.data ?? const <int>[]);
    } on DioException catch (e) {
      throw ApiException.fromDio(e);
    }
  }
}
