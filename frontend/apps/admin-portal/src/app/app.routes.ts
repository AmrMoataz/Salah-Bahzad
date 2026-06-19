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
        path: 'approvals',
        loadComponent: () =>
          import('@sb/admin-portal/feature-students').then((m) => m.ApprovalsQueueComponent),
      },
      {
        path: 'students',
        loadComponent: () =>
          import('@sb/admin-portal/feature-students').then((m) => m.StudentListComponent),
      },
      {
        path: 'students/:id',
        loadComponent: () =>
          import('@sb/admin-portal/feature-students').then((m) => m.StudentDetailComponent),
      },
      {
        path: 'staff',
        loadComponent: () =>
          import('@sb/admin-portal/feature-staff').then((m) => m.StaffListComponent),
      },
      {
        path: 'taxonomy',
        loadComponent: () =>
          import('@sb/admin-portal/feature-taxonomy').then((m) => m.TaxonomyPageComponent),
      },
      {
        path: 'settings',
        loadComponent: () =>
          import('@sb/admin-portal/feature-settings').then((m) => m.SettingsPageComponent),
      },
    ],
  },
  { path: '**', redirectTo: '' },
];
