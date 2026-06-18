import { Routes } from '@angular/router';
import { authGuard, guestGuard } from '@sb/admin-portal/feature-auth';

export const appRoutes: Routes = [
  {
    path: 'login',
    canActivate: [guestGuard],
    loadComponent: () =>
      import('@sb/admin-portal/feature-auth').then((m) => m.LoginComponent),
  },
  {
    path: '',
    canActivate: [authGuard],
    loadComponent: () =>
      import('@sb/admin-portal/feature-shell').then((m) => m.ShellComponent),
    children: [
      {
        path: '',
        redirectTo: 'dashboard',
        pathMatch: 'full',
      },
      {
        path: 'dashboard',
        loadComponent: () =>
          import('@sb/admin-portal/feature-dashboard').then((m) => m.DashboardComponent),
      },
      {
        path: 'staff',
        loadComponent: () =>
          import('@sb/admin-portal/feature-staff').then((m) => m.StaffListComponent),
      },
    ],
  },
  { path: '**', redirectTo: '' },
];
