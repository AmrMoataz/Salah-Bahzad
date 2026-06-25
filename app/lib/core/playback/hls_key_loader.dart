import 'dart:typed_data';

import '../../data/playback_repository.dart';

/// Authenticates the AES-128 key fetch (D3). Native HLS engines request the
/// `#EXT-X-KEY` URI **without** the app's JWT, so an un-augmented key request
/// `401`s — this seam retires that risk: it fetches the 16 key bytes through the
/// [PlaybackRepository] (and therefore the [ApiClient], which injects the
/// `Authorization: Bearer …` from the in-memory `SessionStore` on every
/// `/api/me/*` call and does the single-flight refresh).
///
/// The returned key is **held in memory only** by the caller — never written to
/// disk, the keystore, `SharedPreferences`, or any log (`NFR-APP-SEC-005`). This
/// loader keeps no copy.
class HlsKeyLoader {
  HlsKeyLoader(this._repository);

  final PlaybackRepository _repository;

  /// Returns the raw 16 AES-128 key bytes for [videoId]. Authenticated via the
  /// `ApiClient` Bearer injector; throws an `ApiException` on a gate failure.
  Future<Uint8List> load(String videoId) => _repository.keyBytes(videoId);
}
