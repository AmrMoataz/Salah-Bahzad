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
        // S0 placeholder home (the catalogue/sessions/profile children land in S2/S3/S6).
        path: '',
        loadComponent: () =>
          import('./placeholders/home-placeholder.component').then((m) => m.HomePlaceholderComponent),
      },
      {
        // S0 placeholder for the Redeem FAB target (the enroll modal is S2).
        path: 'redeem',
        loadComponent: () =>
          import('./placeholders/redeem-placeholder.component').then((m) => m.RedeemPlaceholderComponent),
      },
    ],
  },
  { path: '**', redirectTo: '' },
];
