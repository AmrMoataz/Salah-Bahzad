using Mediator;
using SalahBahazad.Application.Features.Quizzes.DTOs;

namespace SalahBahazad.Application.Features.Quizzes.Queries.GetMyQuiz;

/// <summary>
/// The calling student's gating quiz for a session (contract §A #1, FR-PLAT-QZ-001) — the summary the enrol
/// side-effect generated: settings, attempts used/remaining, best-of, pass state, the active attempt id and the
/// per-attempt history. The student is read from the JWT; 404 when the caller has no quiz for the session.
/// </summary>
public sealed record GetMyQuizQuery(Guid SessionId) : IRequest<StudentQuizDto>;
