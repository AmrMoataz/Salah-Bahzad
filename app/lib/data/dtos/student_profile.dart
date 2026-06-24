/// `StudentProfileDto` from `GET /api/me/profile` (contract §C) — the watermark
/// identity source. `serial` is the NEW field (added in A1 on the backend); the
/// watermark renders `"{serial} · {fullName}"`. `boundDevice` is `null` for app
/// sessions. The app reads this in A1+ for the player overlay.
class StudentProfile {
  const StudentProfile({
    required this.id,
    required this.serial,
    required this.fullName,
    required this.phoneNumber,
    required this.parentPhonePrimary,
    this.parentPhoneSecondary,
    required this.schoolName,
    required this.gradeId,
    this.gradeName,
    required this.cityId,
    this.cityName,
    required this.regionId,
    this.regionName,
    required this.status,
    this.boundDevice,
  });

  final String id;
  final String serial;
  final String fullName;
  final String phoneNumber;
  final String parentPhonePrimary;
  final String? parentPhoneSecondary;
  final String schoolName;
  final String gradeId;
  final String? gradeName;
  final String cityId;
  final String? cityName;
  final String regionId;
  final String? regionName;
  final String status;
  final String? boundDevice;

  /// What the dual-layer watermark renders (contract §C): serial + full name.
  String get watermarkLabel => '$serial · $fullName';

  factory StudentProfile.fromJson(Map<String, dynamic> json) {
    return StudentProfile(
      id: json['id'] as String,
      serial: json['serial'] as String? ?? '',
      fullName: json['fullName'] as String? ?? '',
      phoneNumber: json['phoneNumber'] as String? ?? '',
      parentPhonePrimary: json['parentPhonePrimary'] as String? ?? '',
      parentPhoneSecondary: json['parentPhoneSecondary'] as String?,
      schoolName: json['schoolName'] as String? ?? '',
      gradeId: json['gradeId'] as String? ?? '',
      gradeName: json['gradeName'] as String?,
      cityId: json['cityId'] as String? ?? '',
      cityName: json['cityName'] as String?,
      regionId: json['regionId'] as String? ?? '',
      regionName: json['regionName'] as String?,
      status: json['status'] as String? ?? 'Active',
      boundDevice: json['boundDevice'] as String?,
    );
  }
}
