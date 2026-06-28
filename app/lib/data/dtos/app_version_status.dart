/// Contract §F.1 — `GET /api/app/version-status` response.
final class AppVersionStatusDto {
  const AppVersionStatusDto({
    required this.status,
    required this.minVersion,
    required this.latestVersion,
    required this.storeUrl,
  });

  /// `"ok"` | `"update_available"` | `"update_required"`
  final String status;
  final String minVersion;
  final String latestVersion;

  /// The store URL for this platform; empty string when not configured.
  final String storeUrl;

  factory AppVersionStatusDto.fromJson(Map<String, dynamic> json) =>
      AppVersionStatusDto(
        status: json['status'] as String,
        minVersion: json['minVersion'] as String,
        latestVersion: json['latestVersion'] as String,
        storeUrl: json['storeUrl'] as String? ?? '',
      );
}
