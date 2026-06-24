import 'package:flutter/foundation.dart';

/// A parsed Play deep link (contract §E):
/// `salah-bahazad://stream?videoId={..}&sessionId={..}&handoff={..}`.
///
/// `handoff` is the **credential** (a one-time code, never a token); `videoId`
/// and `sessionId` are advisory routing hints. We never read a bearer/refresh
/// token from a URL (`NFR-APP-SEC-004`) — there is no such param, by design.
@immutable
class PlaybackRequest {
  const PlaybackRequest({
    required this.videoId,
    required this.handoff,
    this.sessionId,
  });

  final String videoId;
  final String handoff;
  final String? sessionId;

  /// The custom scheme — note the `bahazad` spelling (distinct from the JWT's
  /// `bahzad`), per contract §E.
  static const String scheme = 'salah-bahazad';
  static const String host = 'stream';

  /// Parses a URI, returning `null` for anything malformed — a wrong scheme,
  /// the wrong host, or a missing `videoId`/`handoff`. The caller routes a
  /// `null` to a clear error state; it must **never** throw (`NFR-APP-REL-002`).
  static PlaybackRequest? tryParse(Uri uri) {
    if (uri.scheme.toLowerCase() != scheme) return null;
    final bool isStream = uri.host.toLowerCase() == host ||
        uri.pathSegments.contains(host) ||
        uri.path == '/$host';
    if (!isStream) return null;

    final String videoId = (uri.queryParameters['videoId'] ?? '').trim();
    final String handoff = (uri.queryParameters['handoff'] ?? '').trim();
    final String sessionId = (uri.queryParameters['sessionId'] ?? '').trim();
    if (videoId.isEmpty || handoff.isEmpty) return null;

    return PlaybackRequest(
      videoId: videoId,
      handoff: handoff,
      sessionId: sessionId.isEmpty ? null : sessionId,
    );
  }

  /// Convenience over [tryParse] for a raw string; tolerant of unparseable
  /// input (returns `null`, never throws).
  static PlaybackRequest? tryParseString(String raw) {
    final Uri? uri = Uri.tryParse(raw.trim());
    if (uri == null) return null;
    return tryParse(uri);
  }

  @override
  bool operator ==(Object other) =>
      other is PlaybackRequest &&
      other.videoId == videoId &&
      other.handoff == handoff &&
      other.sessionId == sessionId;

  @override
  int get hashCode => Object.hash(videoId, handoff, sessionId);

  /// Safe to log — the handoff is redacted.
  @override
  String toString() =>
      'PlaybackRequest(videoId: $videoId, sessionId: $sessionId, handoff: •••)';
}
