# Known Issues & Follow-ups

> A running backlog of issues to fix **later** — captured, not yet fixed. Reported by Amr while testing the
> student-portal assignment/quiz flows against the admin portal + assessment engine. Each item lists the symptom,
> the expected behaviour, and a confirmed starting pointer in the code. Nothing here is implemented yet.
>
> Created 2026-06-22.

---

## 1. Refunded enrollment still appears in the admin session's enrollment list (no status, no activity entry)

- **Area:** Admin portal — session → enrollments list · **Type:** bug / UX · **Stream:** backend + frontend
- **Symptom:** Open a session in the admin panel and view its enrollment list. After refunding an enrollment it
  **still shows in the list** with no indication it was refunded — even though the refund really happened (code
  returned to circulation, enrollment marked `Refunded`).
- **Expected (Amr's call):** keep the row for **traceability**, but then (a) **show the enrollment status**
  (`Refunded`) in the list, **and** (b) **record the refund in the session activity** feed. (Alternative if not for
  traceability: filter refunded rows out of the list — but the financial-history rule argues for keeping + labelling.)
- **Starting pointers:**
  - List query: `Application/Features/Enrollment/Queries/ListSessionEnrollments/` → `EnrollmentListDto`
    (in `Application/Features/Enrollment/DTOs/EnrollmentDtos.cs`) — likely doesn't carry/show `Status`.
  - Status enum already exists: `Domain/Enums/EnrollmentStatus.cs` (has `Refunded`).
  - Session activity: `Application/Features/Sessions/Queries/ListSessionActivity/` — confirm it surfaces
    `EnrollmentRefundedEvent` (`Domain/Events/EnrollmentRefundedEvent.cs`); it currently may not.
  - Admin UI: the session-detail enrollments list component (admin-portal feature for sessions/enrollment).

## 2. Refunding a session does not reset the quiz attempts — student can't retake the quiz

- **Area:** Assessment engine (5B-2) — refund side-effects · **Type:** bug · **Stream:** backend
- **Symptom:** After refunding (and re-enrolling) a student, the **quiz attempts are not restarted**, so if something
  went wrong you can't let the student retake the quiz — the consumed/terminal attempts persist and
  `attemptsRemaining` stays exhausted.
- **Expected:** a refund (or the subsequent re-enroll/unlock) should **reset/regenerate the quiz** for that
  enrollment — clear the attempts and reset `AttemptsUsed` / `BestPercent` / `Passed` — the same way the **assignment
  is already regenerated** on refund→unlock.
- **Notes / root cause:** quiz generation in `EnrollmentSideEffects` is **idempotent — one `UserQuiz` per enrollment**;
  on refund the `UserQuiz` row (with its terminal `QuizAttempt`s) survives, and a terminal attempt never reopens
  (`UserQuiz.TimeOut/Forfeit/Submit` are one-way). So re-enroll reuses the old quiz with spent attempts. (Confirmed live
  during S5 wiring: "un-redoing a consumed attempt needs a fresh enrollment.")
- **Starting pointers:**
  - `Application/Features/Enrollment/Commands/RefundEnrollment/RefundEnrollmentHandler.cs`
  - `Infrastructure/Services/EnrollmentSideEffects.cs` (assignment regen is here; add quiz reset/regen alongside it)
  - `Domain/Entities/UserQuiz.cs` (needs a reset path, or refund should delete+regenerate the `UserQuiz`)

## 3. Quiz runner lets the student keep answering after the attempt has already ended server-side

- **Area:** Student portal (S5) — quiz runner · **Type:** bug / UX · **Stream:** frontend
- **Symptom:** While taking a quiz, **every answer returned a backend error "the attempt is not in progress"**, but the
  runner let the student answer **the whole quiz** and only at the **end** revealed it had been **forfeited / scored 0**.
  (Root trigger is server-side termination mid-sitting — a single-sitting **forfeit-on-disconnect** or a **timeout** —
  after which the answer `PUT` returns **409** each time.)
- **Expected:** the moment the backend says the attempt is **not in progress** (a `409` from `answer`/`submit`), the
  frontend should **immediately take the student out of the quiz** — stop the timer + hub, show a clear "this attempt
  has ended (forfeited / timed out)" message, and route to the results/session — **not** let them grind through every
  question to discover the failure at the end.
- **Starting pointers:**
  - `frontend/libs/student-portal/feature-assessment/src/lib/quiz/quiz-runner.component.ts` — the `answer()` (and
    `submit()`) error handling: on a `409` "not in progress", set a terminal state and navigate out once (don't swallow
    per-answer and continue).
  - `QuizService.answer/submit` in `libs/student-portal/data-access` (the `409` surfaces as `HttpErrorResponse`).
  - Related (already handled): the local-timer-zero → `submit` → `409` → re-fetch `by-session` path in the S5 contract
    `docs/contracts/student-s5-quizzes.md` §C; this item generalises that to **any** mid-quiz 409.

## 4. Attendance drill-in: rich on assignment, thin on quiz; behaviour log is for the assignment but should be the quiz

- **Area:** Admin portal — attendance drill-in / review · **Type:** enhancement · **Stream:** frontend (+ maybe backend)
- **Symptom:** The attendance drill-in shows **a lot about the assignment** but **very little about the quiz**, and the
  **behaviour log is shown for the assignment** (Entered/Navigated/Left). A teacher doesn't care what the student did
  while answering the **assignment** — the behaviour that matters (focus-loss / tab-switches) is on the **proctored
  quiz**.
- **Expected:** the **behaviour log should track/show the QUIZ's** focus events (`FocusLost`/`FocusReturned`), **not**
  the assignment's navigation; and the **quiz section of the drill-in should be richer** (it's currently attempt-level
  scores only).
- **Notes:** the backend **already has a quiz behaviour endpoint** — so #4 is mostly a frontend wiring choice (point the
  behaviour tab at the quiz, de-emphasise/drop the assignment behaviour). Richer quiz detail may also want a
  per-question quiz answer-key for staff (today only the **student** has one — `GET /api/me/quizzes/attempts/{id}/review`;
  staff `QuizReviewDto` is attempt-level only).
- **Starting pointers:**
  - Backend (exists): `Application/Features/Review/Queries/GetQuizBehaviour/` (quiz focus events) vs
    `Application/Features/Review/Queries/GetAssignmentBehaviour/` (assignment nav); routes in
    `Api/Endpoints/ReviewEndpoints.cs` (`/api/review/quizzes/{enrollmentId}/behaviour` and the assignment twin).
  - Quiz review DTO (thin): `QuizReviewDto` in `Application/Features/Review/DTOs/ReviewDtos.cs`.
  - Admin UI: `frontend/libs/admin-portal/feature-attendance/` — the review/drill-in component (the behaviour-log tab is
    wired to the assignment behaviour; switch it to the quiz behaviour and enrich the quiz tab).
