import '../../core/storage/session.dart';
import 'student_summary.dart';

/// `StudentAuthResponse` from `app-exchange` / `refresh` (contract §A.1 / §B).
class StudentAuthResponse {
  const StudentAuthResponse({
    required this.accessToken,
    required this.refreshToken,
    required this.accessTokenExpiresAt,
    required this.refreshTokenExpiresAt,
    required this.student,
  });

  final String accessToken;
  final String refreshToken;
  final DateTime accessTokenExpiresAt;
  final DateTime refreshTokenExpiresAt;
  final StudentSummary student;

  /// The token pair, ready for the keystore.
  Session toSession() => Session(
    accessToken: accessToken,
    refreshToken: refreshToken,
    accessTokenExpiresAt: accessTokenExpiresAt,
    refreshTokenExpiresAt: refreshTokenExpiresAt,
  );

  factory StudentAuthResponse.fromJson(Map<String, dynamic> json) {
    return StudentAuthResponse(
      accessToken: json['accessToken'] as String,
      refreshToken: json['refreshToken'] as String,
      accessTokenExpiresAt: DateTime.parse(
        json['accessTokenExpiresAt'] as String,
      ),
      refreshTokenExpiresAt: DateTime.parse(
        json['refreshTokenExpiresAt'] as String,
      ),
      student: StudentSummary.fromJson(json['student'] as Map<String, dynamic>),
    );
  }
}
