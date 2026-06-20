import { inject } from '@angular/core';
import { CanActivateFn, Router, Routes } from '@angular/router';
import { authGuard, guestGuard } from '@sb/admin-portal/feature-auth';
import { AuthStore } from '@sb/shared/data-access';

/** Allows the route only when the signed-in staff holds `permission`, else bounces to the dashboard. */
const permissionGuard =
  (permission: string): CanActivateFn =>
  () => {
    const auth = inject(AuthStore);
    const router = inject(Router);
    return auth.hasPermission(permission) ? true : router.createUrlTree(['/dashboard']);
  };

/** Teacher-only route gate (the server still enforces the granular permission regardless). */
const teacherGuard: CanActivateFn = () => {
  const auth = inject(AuthStore);
  const router = inject(Router);
  return auth.role() === 'Teacher' ? true : router.createUrlTree(['/dashboard']);
};

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
        path: 'sessions',
        loadComponent: () =>
          import('@sb/admin-portal/feature-sessions').then((m) => m.SessionListComponent),
      },
      {
        path: 'sessions/new',
        loadComponent: () =>
          import('@sb/admin-portal/feature-sessions').then((m) => m.SessionFormComponent),
      },
      {
        path: 'sessions/:id/edit',
        loadComponent: () =>
          import('@sb/admin-portal/feature-sessions').then((m) => m.SessionFormComponent),
      },
      {
        path: 'sessions/:id/quiz-settings',
        loadComponent: () =>
          import('@sb/admin-portal/feature-sessions').then((m) => m.QuizSettingsComponent),
      },
      {
        path: 'sessions/:id/questions/new',
        loadComponent: () =>
          import('@sb/admin-portal/feature-sessions').then((m) => m.QuestionEditorComponent),
      },
      {
        path: 'sessions/:id/questions/:questionId/edit',
        loadComponent: () =>
          import('@sb/admin-portal/feature-sessions').then((m) => m.QuestionEditorComponent),
      },
      {
        path: 'sessions/:id',
        loadComponent: () =>
          import('@sb/admin-portal/feature-sessions').then((m) => m.SessionDetailComponent),
      },
      {
        path: 'codes',
        canActivate: [permissionGuard('CodesRead')],
        loadComponent: () =>
          import('@sb/admin-portal/feature-codes').then((m) => m.CodeListComponent),
      },
      {
        path: 'codes/generate',
        canActivate: [permissionGuard('CodesRead'), teacherGuard],
        loadComponent: () =>
          import('@sb/admin-portal/feature-codes').then((m) => m.CodesGenerateComponent),
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
