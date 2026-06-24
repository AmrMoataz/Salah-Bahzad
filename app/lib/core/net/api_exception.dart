import 'package:dio/dio.dart';

/// A typed transport error carrying the bits the UI maps to states: the HTTP
/// [statusCode], the ProblemDetails `reason` extension (contract §A.2 / §D.2),
/// and the human `detail` the app renders inline.
class ApiException implements Exception {
  ApiException({
    required this.statusCode,
    this.reason,
    this.detail,
    this.isNetwork = false,
  });

  /// `null` for a pure network/connectivity failure (no response).
  final int? statusCode;

  /// The lowercase `reason` code (e.g. `account_pending`, `no_views_remaining`).
  final String? reason;

  /// The server's `detail` string, surfaced verbatim where the design calls for
  /// it (e.g. a rejection reason).
  final String? detail;

  final bool isNetwork;

  bool get isServerError => statusCode != null && statusCode! >= 500;

  /// Builds an [ApiException] from a Dio failure, extracting `reason`/`detail`
  /// from the ProblemDetails body when present.
  factory ApiException.fromDio(DioException e) {
    final Response<dynamic>? res = e.response;
    if (res == null) {
      // No response → connectivity / timeout / TLS handshake failure.
      return ApiException(statusCode: null, isNetwork: true);
    }
    String? reason;
    String? detail;
    final dynamic data = res.data;
    if (data is Map) {
      final dynamic r = data['reason'];
      final dynamic d = data['detail'];
      if (r is String) reason = r;
      if (d is String) detail = d;
    }
    return ApiException(
      statusCode: res.statusCode,
      reason: reason,
      detail: detail,
    );
  }

  @override
  String toString() =>
      'ApiException(status: $statusCode, reason: $reason, network: $isNetwork)';
}
