import { Routes } from '@angular/router';
import { authGuard, guestGuard, statusGuard } from '@sb/student-portal/data-access';

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
        // S0 placeholder home (the sessions/profile children land in S3/S6).
        path: '',
        loadComponent: () =>
          import('./placeholders/home-placeholder.component').then((m) => m.HomePlaceholderComponent),
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
