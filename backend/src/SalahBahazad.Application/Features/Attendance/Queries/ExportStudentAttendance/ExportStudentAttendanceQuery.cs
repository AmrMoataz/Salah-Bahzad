using Mediator;
using SalahBahazad.Application.Features.Attendance.DTOs;

namespace SalahBahazad.Application.Features.Attendance.Queries.ExportStudentAttendance;

/// <summary>
/// Exports a student's whole per-session attendance breakdown as CSV (contract §B #6, FR-ADM-ATT-004) — same
/// cohort as the breakdown (#5). Audited server-side: a GET the SaveChanges interceptor cannot capture.
/// </summary>
public sealed record ExportStudentAttendanceQuery(Guid StudentId) : IRequest<AttendanceCsvFile>;
