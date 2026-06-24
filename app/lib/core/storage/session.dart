import 'dart:convert';

/// The persisted session: the bearer pair + their expiries. **Never** logged,
/// **only** stored in the OS keystore (`NFR-APP-SEC-001/004`).
class Session {
  const Session({
    required this.accessToken,
    required this.refreshToken,
    required this.accessTokenExpiresAt,
    required this.refreshTokenExpiresAt,
  });

  final String accessToken;
  final String refreshToken;
  final DateTime accessTokenExpiresAt;
  final DateTime refreshTokenExpiresAt;

  bool get isAccessExpired =>
      DateTime.now().toUtc().isAfter(accessTokenExpiresAt.toUtc());

  bool get isRefreshExpired =>
      DateTime.now().toUtc().isAfter(refreshTokenExpiresAt.toUtc());

  Session copyWith({
    String? accessToken,
    String? refreshToken,
    DateTime? accessTokenExpiresAt,
    DateTime? refreshTokenExpiresAt,
  }) {
    return Session(
      accessToken: accessToken ?? this.accessToken,
      refreshToken: refreshToken ?? this.refreshToken,
      accessTokenExpiresAt: accessTokenExpiresAt ?? this.accessTokenExpiresAt,
      refreshTokenExpiresAt:
          refreshTokenExpiresAt ?? this.refreshTokenExpiresAt,
    );
  }

  String toJson() => jsonEncode(<String, dynamic>{
    'accessToken': accessToken,
    'refreshToken': refreshToken,
    'accessTokenExpiresAt': accessTokenExpiresAt.toUtc().toIso8601String(),
    'refreshTokenExpiresAt': refreshTokenExpiresAt.toUtc().toIso8601String(),
  });

  static Session? tryParse(String? raw) {
    if (raw == null || raw.isEmpty) return null;
    try {
      final dynamic m = jsonDecode(raw);
      if (m is! Map) return null;
      final String? a = m['accessToken'] as String?;
      final String? r = m['refreshToken'] as String?;
      final String? ax = m['accessTokenExpiresAt'] as String?;
      final String? rx = m['refreshTokenExpiresAt'] as String?;
      if (a == null || r == null || ax == null || rx == null) return null;
      return Session(
        accessToken: a,
        refreshToken: r,
        accessTokenExpiresAt: DateTime.parse(ax),
        refreshTokenExpiresAt: DateTime.parse(rx),
      );
    } catch (_) {
      return null;
    }
  }
}
