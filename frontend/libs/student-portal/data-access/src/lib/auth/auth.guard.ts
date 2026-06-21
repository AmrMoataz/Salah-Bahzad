import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { StudentAuthStore } from './student-auth.store';

/**
 * Protects the authenticated student shell. Redirects to `/login` when not signed in, after
 * attempting to restore a session from sessionStorage (browser refresh).
 */
export const authGuard: CanActivateFn = () => {
  const authStore = inject(StudentAuthStore);
  const router = inject(Router);

  if (authStore.isAuthenticated()) return true;
  if (authStore.restoreSession()) return true;

  return router.createUrlTree(['/login']);
};

/** Prevents already-signed-in students from reaching `/login` — sends them to the shell home. */
export const guestGuard: CanActivateFn = () => {
  const authStore = inject(StudentAuthStore);
  const router = inject(Router);

  if (!authStore.isAuthenticated() && !authStore.restoreSession()) return true;

  return router.createUrlTree(['/']);
};

/**
 * Allows the status screen only when a blocked-sign-in reason is parked (pending / rejected /
 * inactive / device_not_recognized). A direct hit on `/status` with no parked reason bounces to
 * `/login`.
 */
export const statusGuard: CanActivateFn = () => {
  const authStore = inject(StudentAuthStore);
  const router = inject(Router);

  return authStore.status() !== null ? true : router.createUrlTree(['/login']);
};
