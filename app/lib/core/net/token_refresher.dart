import 'package:dio/dio.dart';

import '../logging/logging.dart';
import '../storage/session.dart';

/// Exchanges a refresh token for a fresh token pair (contract §B,
/// `POST /api/auth/refresh`). Uses a **bare** Dio with **no** auth/refresh
/// interceptor, so a 401 here can never recurse into another refresh.
///
/// App tokens carry no `device_id`; the server skips the device re-check for
/// them (contract §B "made app-aware"). Only the token pair is consumed here —
/// the student summary on the response is ignored (unchanged across a refresh).
class TokenRefresher {
  TokenRefresher(this._bareDio, {AppLogger? logger})
    : _log = logger ?? Log.scoped('net');

  final Dio _bareDio;
  final AppLogger _log;

  /// Returns the new [Session], or `null` when the refresh token is
  /// expired/invalid (server `401`) or the network is down — the caller then
  /// forces a clean sign-out.
  Future<Session?> refresh(String refreshToken) async {
    try {
      final Response<dynamic> res = await _bareDio.post<dynamic>(
        '/api/auth/refresh',
        data: <String, dynamic>{'refreshToken': refreshToken},
      );
      final dynamic data = res.data;
      if (data is! Map) return null;
      final String? access = data['accessToken'] as String?;
      final String? refresh = data['refreshToken'] as String?;
      final String? accessExp = data['accessTokenExpiresAt'] as String?;
      final String? refreshExp = data['refreshTokenExpiresAt'] as String?;
      if (access == null ||
          refresh == null ||
          accessExp == null ||
          refreshExp == null) {
        _log.warning('Refresh response missing token fields');
        return null;
      }
      return Session(
        accessToken: access,
        refreshToken: refresh,
        accessTokenExpiresAt: DateTime.parse(accessExp),
        refreshTokenExpiresAt: DateTime.parse(refreshExp),
      );
    } on DioException catch (e) {
      // Expected on an expired/invalid refresh token (401) or no connectivity;
      // the caller turns null into a clean sign-out. Never logs the token.
      _log.debug(
        'Refresh rejected',
        fields: <String, Object?>{'status': e.response?.statusCode},
      );
      return null;
    }
  }
}
