/// `PlaybackManifestDto` from D2 `POST /api/me/videos/playback/redeem`
/// (contract §D.1). Redeeming a handoff returns the **rewritten** HLS manifest:
/// signed R2 segment URLs (~120 s TTL) plus an absolute key URL baked into the
/// `#EXT-X-KEY METHOD=AES-128,URI="…",IV=0x…` tag. **No view is spent on
/// redeem** (the view was spent at D1).
///
/// Every field here — the manifest text, the signed segment URLs inside it, and
/// the key URL — is **memory-only** and must never be written to disk, the
/// keystore, `SharedPreferences`, or a log (`NFR-APP-SEC-005`). [toString] is
/// redacted accordingly.
class PlaybackManifest {
  const PlaybackManifest({
    required this.manifestContent,
    required this.keyUrl,
    required this.expiresAtUtc,
    this.accessRemaining,
    this.accessAllowed,
    this.videoTitle,
    this.watermark,
  });

  /// The rewritten `.m3u8` text (signed segment URLs + the absolute key URI).
  final String manifestContent;

  /// Absolute URL to D3 `GET /api/me/videos/{id}/hls.key` (the AES-128 key).
  final String keyUrl;

  /// ISO-8601 offset. The signed segment URLs / handoff are dead after this —
  /// a retry must reuse the manifest only while it is still valid (§D.3).
  final DateTime expiresAtUtc;

  /// The per-video view budget surfaced for "N of M views left" (`FR-APP-VID-004`,
  /// contract §D). The gate already spent this view, so [accessRemaining] is the
  /// post-Play count (the "N") and [accessAllowed] is the total granted (the "M").
  /// Nullable for resilience to an older API that omits them → the counter falls
  /// back to a non-numeric chip.
  final int? accessRemaining;
  final int? accessAllowed;

  /// The SessionVideo's own title (contract §D) — the player's top-bar title.
  /// Null on an older API → the player falls back to a neutral label.
  final String? videoTitle;

  /// The bound student's "{serial} · {fullName}" watermark identity (contract §D,
  /// FR-APP-VID-003), carried per-playback so the overlay is always present and
  /// never depends on a separate `/api/me/profile` fetch. Never the phone.
  final String? watermark;

  bool isExpiredAt(DateTime nowUtc) => !nowUtc.isBefore(expiresAtUtc.toUtc());

  factory PlaybackManifest.fromJson(Map<String, dynamic> json) {
    return PlaybackManifest(
      manifestContent: json['manifestContent'] as String? ?? '',
      keyUrl: json['keyUrl'] as String? ?? '',
      expiresAtUtc: DateTime.parse(json['expiresAtUtc'] as String),
      accessRemaining: json['accessRemaining'] as int?,
      accessAllowed: json['accessAllowed'] as int?,
      videoTitle: json['videoTitle'] as String?,
      watermark: json['watermark'] as String?,
    );
  }

  /// Safe to log — the manifest body, signed URLs and key URL are all redacted
  /// (they are secrets under `NFR-APP-SEC-005`).
  @override
  String toString() => 'PlaybackManifest(content: <redacted>, keyUrl: '
      '<redacted>, expiresAtUtc: ${expiresAtUtc.toUtc().toIso8601String()})';
}
