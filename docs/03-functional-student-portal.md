# Functional Requirements — Student Portal

The student's entire interface to the platform: register, get approved, bind a device, enroll via code, study, sit assignments and quizzes, and manage a profile. Shared engines (auth, enrollment, assessment, video, audit) are in [01 — Platform/shared](01-functional-platform-shared.md).

## Contents

- [A. Information architecture & screen inventory](#a-information-architecture--screen-inventory)
- [B. Registration & onboarding](#b-registration--onboarding)
- [C. Sign-in & device linking](#c-sign-in--device-linking)
- [D. Catalogue & enrollment](#d-catalogue--enrollment)
- [E. My sessions & secure video](#e-my-sessions--secure-video)
- [F. Assignments](#f-assignments)
- [G. Quizzes](#g-quizzes)
- [H. Profile](#h-profile)
- [I. Responsiveness & accessibility](#i-responsiveness--accessibility)

---

## A. Information architecture & screen inventory

A guarded shell (header + side nav) wraps every authenticated route; registration and sign-in are outside it. Responsive across phone, tablet, and desktop.

| # | Screen | Purpose | Key elements | Satisfies |
|---|---|---|---|---|
| 1 | **Sign in** | Authenticate | Firebase email/password + Google; links to register | FR-STU-AUTH-001 |
| 2 | **Register (wizard)** | Create account | Step 1 Firebase identity → Step 2 school/grade/city→region/parents/ID upload/terms → submit → pending | FR-STU-REG-001..008 |
| 3 | **Pending / status** | Post-submit state | "Awaiting approval" or "Rejected: \<reason\>" with next steps | FR-STU-REG-009 |
| 4 | **Device link prompt** | Bind device | Consent dialog on first trusted sign-in | FR-STU-DEV-001..002 |
| 5 | **Home / catalogue** | Discover sessions | Filter by grade/subject/specialization, session cards (price, prerequisite badge), enroll CTA | FR-STU-CAT-001..002 |
| 6 | **Enroll modal** | Redeem a code | Stepped code entry (segmented input + paste), validation, success | FR-STU-CAT-003..004 |
| 7 | **My sessions** | Enrolled content | Session list with progress, expiry countdown; open session | FR-STU-SES-001 |
| 8 | **Session detail** | Study one session | Video playlist (access-count + lock states), materials, assignment & quiz entry points, prerequisite status | FR-STU-SES-002..004 |
| 9 | **Video player (native app)** | Watch a lesson | Play hands off to the device app (black-out); watermark, access-count feedback, failure states | FR-STU-VID-001..005 |
| 10 | **Assignment** | Do homework | One-question-at-a-time, persistent answers, resumable timer, per-question hint, submit | FR-STU-ASG-001..006 |
| 11 | **Assignment review** | See results | Questions, your vs correct answers, score | FR-STU-ASG-007 |
| 12 | **Quiz intro** | Start an attempt | Rules, attempts remaining, best score, time, "start" warning | FR-STU-QZ-001..002 |
| 13 | **Quiz runner** | Sit the quiz | Single-sitting, countdown, navigation, anti-cheat warnings, auto/manual submit | FR-STU-QZ-003..007 |
| 14 | **Quiz results / attempts** | See attempts | Per-attempt score, best-of, review | FR-STU-QZ-008..009 |
| 15 | **Profile** | Manage account | Personal info, parent numbers, avatar, bound-device info | FR-STU-PRO-001..003 |

## B. Registration & onboarding

| ID | Requirement | Notes |
|---|---|---|
| FR-STU-REG-001 | A prospective student SHALL begin registration by authenticating with Firebase (email/password or a social provider). | Identity first. |
| FR-STU-REG-002 | Where the provider supplies them, the system SHALL pre-fill name, email, profile image, and phone number; the student MAY edit these. | Provider bootstrap. |
| FR-STU-REG-003 | The wizard SHALL collect: **school**, **grade** (from the tenant's grade list), **city** and **region** (region options dependent on the chosen city). | Cascading location. |
| FR-STU-REG-004 | The wizard SHALL collect **two parent/guardian phone numbers** for supervision; at least one SHALL be required. | Parental contact. |
| FR-STU-REG-005 | The wizard SHALL require uploading an **identification image**. | ID verification. |
| FR-STU-REG-006 | The wizard SHALL require explicit acceptance of **terms & conditions**, recording the version, timestamp, and that consent. | Consent record. |
| FR-STU-REG-007 | The student SHALL also be able to register without a social provider (plain email/password). | No-provider path. |
| FR-STU-REG-008 | On submit, the account SHALL be created as **Pending** and SHALL NOT be able to sign in until staff approve it. | Approval gate. |
| FR-STU-REG-009 | A pending student SHALL see an "awaiting approval" state; a rejected student SHALL see the rejection **reason** and guidance. | Status feedback. |

## C. Sign-in & device linking

| ID | Requirement | Notes |
|---|---|---|
| FR-STU-AUTH-001 | Active students SHALL sign in via Firebase; the portal SHALL exchange the verified token for a platform session. | See `FR-PLAT-AUTH-002`. |
| FR-STU-DEV-001 | On first sign-in with no bound device, the student SHALL be prompted to **link this device** to their account, with a clear explanation that one device is allowed. | Consent. |
| FR-STU-DEV-002 | After binding, sign-in or content access from a different device SHALL be blocked with a message to contact support to reset the device. | Enforcement. |
| FR-STU-DEV-003 | The student SHALL see which device is currently bound (and the bind date) in their profile. | Transparency. |

## D. Catalogue & enrollment

| ID | Requirement | Notes |
|---|---|---|
| FR-STU-CAT-001 | Students SHALL browse the published catalogue, filtered by grade/subject/specialization, seeing price, description, and whether a prerequisite applies. | Discovery. |
| FR-STU-CAT-002 | A session requiring a prerequisite SHALL clearly indicate it and SHALL prevent enrollment until the prerequisite is satisfied. | Gate visibility. |
| FR-STU-CAT-003 | Students SHALL enroll by entering a **code** in a guided modal (segmented input with paste support), with inline validation. | Code redemption. |
| FR-STU-CAT-004 | On success, the session SHALL move to **My Sessions**, the code SHALL be consumed, and the assignment/quiz/video access SHALL be provisioned. | Enrollment side-effects per `FR-PLAT-ENR-005`. |
| FR-STU-CAT-005 | Enrollment failures (invalid/used/disabled code, price mismatch, prerequisite unmet, already enrolled) SHALL each show a specific message. | Clear errors. |

## E. My sessions & secure video

| ID | Requirement | Notes |
|---|---|---|
| FR-STU-SES-001 | Students SHALL see their enrolled sessions with progress and an **expiry countdown**; after expiry the videos (and quiz) SHALL be locked while the **assignment remains accessible**. | Validity window; assignment stays open. |
| FR-STU-SES-002 | Within a session, students SHALL see the ordered **video playlist**, each video showing remaining access count and lock state. | View budget. |
| FR-STU-SES-003 | Students SHALL download/read the session's **materials**. | Readings. |
| FR-STU-SES-004 | Students SHALL reach the session's **assignment** and (where applicable) **quiz** from the session detail. | Entry points. |
| FR-STU-VID-001 | Starting a video SHALL be allowed only if the enrollment is active, the video has remaining access, and (where the session has a gating quiz) the quiz has been passed; each start SHALL consume one access and be recorded. | Server-gated. |
| FR-STU-VID-002 | The player SHALL present the video with a **dynamic visible watermark** identifying the student. | Deterrence/traceability. |
| FR-STU-VID-003 | Clicking **Play** SHALL open the lesson in the device's **native app** (via a device-aware deep link); protected playback with the screenshot/recording **black-out** happens there, not in the browser. If the app isn't installed, the portal SHALL prompt to install it. | Black-out lives in the app; see [08](08-functional-app.md). |
| FR-STU-VID-004 | When access is exhausted or the session expired/forbidden, playback SHALL fail with a specific reason and remaining-count feedback. | Failure UX. |
| FR-STU-VID-005 | Until the mobile app ships, mobile-web playback SHALL be watermarked HLS **without** the black-out (capturable); desktop playback uses the desktop app. | Interim trade-off. |

## F. Assignments

| ID | Requirement | Notes |
|---|---|---|
| FR-STU-ASG-001 | Students SHALL open a session's assignment at any time and answer questions **one at a time**, with answers saved as they go. | Open-book, incremental. |
| FR-STU-ASG-002 | Students SHALL leave and resume the assignment freely; there SHALL be no single-sitting constraint. | Resumable. |
| FR-STU-ASG-003 | The assignment SHALL show **accumulated time spent**, resuming the timer on re-entry. | Time tracking. |
| FR-STU-ASG-004 | Each question MAY show a **hint** (e.g. a YouTube explainer) when one is configured. | Assignment-only hint. |
| FR-STU-ASG-005 | LaTeX and image-based questions SHALL render correctly. | Math rendering. |
| FR-STU-ASG-006 | When all questions are answered, the assignment SHALL submit, auto-grade, and update the student's progress/score. | Completion. |
| FR-STU-ASG-007 | After completion, students SHALL review their answers vs. correct answers and their score. | Review. |

## G. Quizzes

| ID | Requirement | Notes |
|---|---|---|
| FR-STU-QZ-001 | The quiz intro SHALL show the rules, time limit, attempts remaining, and current best score before starting. | Informed start. |
| FR-STU-QZ-002 | Starting an attempt SHALL warn the student that it must be completed in **one sitting** and that leaving forfeits the attempt. | Explicit warning. |
| FR-STU-QZ-003 | A started attempt SHALL run a visible **countdown**; the authoritative timer is server-side. | Timer. |
| FR-STU-QZ-004 | Closing the browser, navigating away, or losing connection SHALL **forfeit** the active attempt (zero score) and consume it. | Strict single-sitting. |
| FR-STU-QZ-005 | Switching tab/window or losing focus SHALL be detected and **recorded for staff review** — it does **not** forfeit the attempt; the student MAY be shown a notice that the event is logged. | Monitored, not auto-forfeit. |
| FR-STU-QZ-006 | When the timer expires, the attempt SHALL auto-submit with whatever was answered. | Auto-submit. |
| FR-STU-QZ-007 | Questions SHALL be randomised per attempt (subset + variation selection); LaTeX/image questions SHALL render. | Randomisation. |
| FR-STU-QZ-008 | After submission, the student SHALL see the attempt score and their **best-of** score across attempts. | Best-of. |
| FR-STU-QZ-009 | Students SHALL review each attempt's questions, their answers, and the correct answers; they SHALL NOT be able to re-open a submitted attempt. | Review, immutable. |
| FR-STU-QZ-010 | Passing (best score ≥ minimum pass %) SHALL unlock this session's **videos**. | Gate satisfied. |

## H. Profile

| ID | Requirement | Notes |
|---|---|---|
| FR-STU-PRO-001 | Students SHALL view/update personal info (name, contact, school, city/region, grade where permitted) and avatar. | Self-service. |
| FR-STU-PRO-002 | Students SHALL view/update the two parent/guardian phone numbers. | Parent contacts. |
| FR-STU-PRO-003 | Students SHALL change their password (or manage it via the provider) and see their bound-device info. | Security self-service. |

## I. Responsiveness & accessibility

| ID | Requirement | Notes |
|---|---|---|
| FR-STU-RWD-001 | The portal SHALL be fully responsive across phone, tablet, and desktop, with layouts that adapt to each breakpoint and every feature usable on each. | Phone/tablet/desktop are first-class. |
| FR-STU-RWD-002 | On touch devices, controls SHALL have touch-appropriate targets and navigation; no feature SHALL be desktop-only. | Touch usability. |
| FR-STU-A11Y-001 | Interactive elements SHALL be keyboard-navigable and screen-reader-labelled to meet the accessibility NFRs. | See `NFR-A11Y-*`. |

---

➡️ Next: [04 — Non-functional requirements](04-non-functional-requirements.md)
