using Mediator;
using SalahBahazad.Application.Features.Attendance.DTOs;

namespace SalahBahazad.Application.Features.Attendance.Queries.ExportSessionAttendance;

/// <summary>
/// Exports a session's whole attendance cohort as CSV (contract §B #6, FR-ADM-ATT-004) — same cohort as the
/// matrix (#4). Audited server-side: a GET the SaveChanges interceptor cannot capture.
/// </summary>
public sealed record ExportSessionAttendanceQuery(Guid SessionId) : IRequest<AttendanceCsvFile>;
