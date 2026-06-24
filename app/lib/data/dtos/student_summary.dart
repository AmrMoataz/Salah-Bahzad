/// The `student` object on `StudentAuthResponse` (contract §A.1).
///
/// `boundDevice` is **always `null`** for the app — it is device-agnostic
/// (contract §0 / §A): no binding, no `device_id`.
class StudentSummary {
  const StudentSummary({
    required this.id,
    required this.fullName,
    required this.status,
    this.boundDevice,
  });

  final String id;
  final String fullName;

  /// Enum **name** over the wire: `Active` / `Pending` / `Rejected` / `Inactive`.
  final String status;

  /// Always `null` for app sessions.
  final String? boundDevice;

  factory StudentSummary.fromJson(Map<String, dynamic> json) {
    return StudentSummary(
      id: json['id'] as String,
      fullName: json['fullName'] as String? ?? '',
      status: json['status'] as String? ?? 'Active',
      boundDevice: json['boundDevice'] as String?,
    );
  }

  Map<String, dynamic> toJson() => <String, dynamic>{
        'id': id,
        'fullName': fullName,
        'status': status,
        'boundDevice': boundDevice,
      };

  /// First name for the greeting ("Welcome back, {firstName}").
  String get firstName => fullName.trim().split(RegExp(r'\s+')).first;
}
