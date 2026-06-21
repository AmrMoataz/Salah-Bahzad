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
