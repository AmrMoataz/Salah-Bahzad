import { Routes } from '@angular/router';
import { authGuard, guestGuard, statusGuard } from '@sb/student-portal/data-access';
import { quizLeaveGuard } from '@sb/student-portal/feature-assessment';

export const appRoutes: Routes = [
  {
    path: 'login',
    canActivate: [guestGuard],
    loadComponent: () =>
      import('@sb/student-portal/feature-auth').then((m) => m.LoginComponent),
  },
  {
    // S1 — the two-step self-registration wizard (guests only).
    path: 'register',
    canActivate: [guestGuard],
    loadComponent: () =>
      import('@sb/student-portal/feature-auth').then((m) => m.RegisterComponent),
  },
  {
    // Blocked-sign-in status screen (pending / rejected / inactive / device_not_recognized).
    path: 'status',
    canActivate: [statusGuard],
    loadComponent: () =>
      import('@sb/student-portal/feature-auth').then((m) => m.StatusComponent),
  },
  {
    path: '',
    canActivate: [authGuard],
    loadComponent: () =>
      import('@sb/student-portal/feature-shell').then((m) => m.ShellComponent),
    children: [
      {
        // Home — the personalized weekly study plan (replaces the S0 placeholder).
        path: '',
        loadComponent: () =>
          import('@sb/student-portal/feature-home').then((m) => m.HomeComponent),
      },
      {
        // S2 — the catalogue discovery screen + enroll-by-code modal.
        path: 'catalogue',
        loadComponent: () =>
          import('@sb/student-portal/feature-catalogue').then((m) => m.CatalogueComponent),
      },
      {
        // S3 — the My Sessions hub (spotlight layout).
        path: 'sessions',
        loadComponent: () =>
          import('@sb/student-portal/feature-sessions').then((m) => m.MySessionsComponent),
      },
      {
        // S3 — the session-detail study screen + deep-link Play (the `:id` is bound via input binding).
        path: 'sessions/:id',
        loadComponent: () =>
          import('@sb/student-portal/feature-sessions').then((m) => m.SessionDetailComponent),
      },
      {
        // S4 — the open-book Assignment runner (reached from the S3 detail card; `:id` via input binding).
        path: 'sessions/:id/assignment',
        loadComponent: () =>
          import('@sb/student-portal/feature-assessment').then((m) => m.AssignmentRunnerComponent),
      },
      {
        // S4 — the answer-key review (`Completed` only; `:id` via input binding).
        path: 'sessions/:id/assignment/review',
        loadComponent: () =>
          import('@sb/student-portal/feature-assessment').then((m) => m.AssignmentReviewComponent),
      },
      {
        // S5 — the proctored quiz intro + runner (one route, phase-switched; `:id` via input binding).
        // The CanDeactivate guard raises the "Leave the quiz?" forfeit confirm on an in-app leave mid-sitting.
        path: 'sessions/:id/quiz',
        canDeactivate: [quizLeaveGuard],
        loadComponent: () =>
          import('@sb/student-portal/feature-assessment').then((m) => m.QuizIntroComponent),
      },
      {
        // S5 — the score-only results screen (reached from the runner / a refresh re-derives it).
        path: 'sessions/:id/quiz/results',
        loadComponent: () =>
          import('@sb/student-portal/feature-assessment').then((m) => m.QuizResultsComponent),
      },
      {
        // S5 — the NEW per-attempt answer-key review (`:id` + `:attemptId` via input binding).
        path: 'sessions/:id/quiz/attempts/:attemptId/review',
        loadComponent: () =>
          import('@sb/student-portal/feature-assessment').then((m) => m.QuizReviewComponent),
      },
      {
        // S6 — the student self-service Profile screen (the FINAL student-portal slice, closes S0..S6).
        path: 'profile',
        loadComponent: () =>
          import('@sb/student-portal/feature-profile').then((m) => m.ProfileComponent),
      },
      {
        // The shell's Redeem FAB target: the catalogue with the enroll modal auto-opened (blank).
        // Routed (not imported) so the shell never depends on feature-catalogue (module boundary).
        path: 'redeem',
        data: { openRedeem: true },
        loadComponent: () =>
          import('@sb/student-portal/feature-catalogue').then((m) => m.CatalogueComponent),
      },
    ],
  },
  { path: '**', redirectTo: '' },
];
