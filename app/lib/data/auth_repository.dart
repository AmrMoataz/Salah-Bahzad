import 'package:dio/dio.dart';

import '../core/net/api_client.dart';
import '../core/net/api_exception.dart';
import 'dtos/student_auth_response.dart';
import 'dtos/student_profile.dart';

/// The app↔backend auth surface (contract §A / §C). The app **only** ever calls
/// the device-agnostic `app-exchange` — never the portal's device-bound
/// `/api/auth/student/exchange`.
class AuthRepository {
  AuthRepository(this._client);

  final ApiClient _client;

  /// `POST /api/auth/student/app-exchange` (contract §A). Firebase ID token →
  /// device-agnostic platform session. `403 {reason}` / `401` / `429` surface
  /// as a typed [ApiException].
  Future<StudentAuthResponse> appExchange(String firebaseIdToken) async {
    try {
      final Response<dynamic> res = await _client.dio.post<dynamic>(
        '/api/auth/student/app-exchange',
        data: <String, dynamic>{'firebaseIdToken': firebaseIdToken},
      );
      return StudentAuthResponse.fromJson(
        (res.data as Map).cast<String, dynamic>(),
      );
    } on DioException catch (e) {
      throw ApiException.fromDio(e);
    }
  }

  /// `GET /api/me/profile` (contract §C) — the watermark identity source
  /// (used by the player in A1; available now for the Idle greeting).
  Future<StudentProfile> me() async {
    try {
      final Response<dynamic> res = await _client.dio.get<dynamic>(
        '/api/me/profile',
      );
      return StudentProfile.fromJson((res.data as Map).cast<String, dynamic>());
    } on DioException catch (e) {
      throw ApiException.fromDio(e);
    }
  }
}
