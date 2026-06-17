import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthStore } from '@sb/shared/data-access';

/**
 * Protects authenticated routes. Redirects to /login if not signed in.
 * Also attempts to restore session from sessionStorage on first load.
 */
export const authGuard: CanActivateFn = () => {
  const authStore = inject(AuthStore);
  const router = inject(Router);

  if (authStore.isAuthenticated()) return true;

  // Try restoring from sessionStorage (browser refresh)
  if (authStore.restoreSession()) return true;

  return router.createUrlTree(['/login']);
};

/**
 * Prevents already-authenticated users from reaching the login page.
 */
export const guestGuard: CanActivateFn = () => {
  const authStore = inject(AuthStore);
  const router = inject(Router);

  if (!authStore.isAuthenticated() && !authStore.restoreSession()) return true;

  return router.createUrlTree(['/dashboard']);
};
