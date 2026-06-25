/// `PlaybackHandoffDto` from D1 `POST /api/me/videos/{videoId}/playback`
/// (contract §D.1) — the ~60 s single-use handoff that **spends one view**.
///
/// On the deep-link path the app never mints this itself (the portal does, then
/// deep-links the `handoffCode`); it is only produced when the app calls D1
/// directly. The `handoffCode` is a **credential** — like the deep-link handoff
/// it is redacted in [toString] and never logged (`NFR-APP-SEC-003`).
class PlaybackHandoff {
  const PlaybackHandoff({required this.handoffCode, required this.expiresAtUtc});

  /// 48-hex one-time code (single-use, Redis GETDEL server-side).
  final String handoffCode;

  /// ISO-8601 offset; the handoff is dead after this instant.
  final DateTime expiresAtUtc;

  bool isExpiredAt(DateTime nowUtc) => !nowUtc.isBefore(expiresAtUtc.toUtc());

  factory PlaybackHandoff.fromJson(Map<String, dynamic> json) {
    return PlaybackHandoff(
      handoffCode: json['handoffCode'] as String? ?? '',
      expiresAtUtc: DateTime.parse(json['expiresAtUtc'] as String),
    );
  }

  /// Safe to log — the handoff code is redacted.
  @override
  String toString() => 'PlaybackHandoff(handoff: •••, expiresAtUtc: '
      '${expiresAtUtc.toUtc().toIso8601String()})';
}
