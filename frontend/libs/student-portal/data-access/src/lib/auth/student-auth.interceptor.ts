import { HttpErrorResponse, HttpInterceptorFn, HttpRequest } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, switchMap, throwError } from 'rxjs';
import { StudentAuthStore } from './student-auth.store';

/** Clones a request with the bearer token and `withCredentials` so the `sb_device` cookie rides. */
function authorize(req: HttpRequest<unknown>, token: string | null): HttpRequest<unknown> {
  return req.clone({
    withCredentials: true,
    ...(token ? { setHeaders: { Authorization: `Bearer ${token}` } } : {}),
  });
}

/**
 * Attaches the Student platform JWT to every API request, sets `withCredentials` on every `/api`
 * call so the HttpOnly `sb_device` cookie travels (device enforcement on future content routes),
 * and transparently recovers from access-token expiry: on a 401 it refreshes once (concurrent 401s
 * share a single refresh) and replays the original request with the new token. If the refresh
 * itself fails, the store clears the session and routes to `/login`, and the original error
 * propagates.
 *
 * The auth endpoints (`/api/auth/*`) are skipped entirely — they carry no bearer, and the store
 * sets its own `withCredentials` on the exchange/refresh; skipping them also keeps the refresh call
 * from recursing into this 401 handler. The **anonymous registration surface** is skipped for the
 * same reason: `POST /api/students/register` and the `/api/reference/*` dropdown reads run before a
 * student exists (no bearer, no cookie, no refresh replay on a 401). Functional interceptor
 * (Angular 20 pattern).
 */
const ANONYMOUS_PATHS = ['/api/auth/', '/api/students/register', '/api/reference/'];

export const studentAuthInterceptor: HttpInterceptorFn = (req, next) => {
  const authStore = inject(StudentAuthStore);

  if (ANONYMOUS_PATHS.some((path) => req.url.includes(path))) {
    return next(req);
  }

  const token = authStore.getAccessToken();

  return next(authorize(req, token)).pipe(
    catchError((error: unknown) => {
      const isUnauthorized = error instanceof HttpErrorResponse && error.status === 401;
      if (!isUnauthorized || !authStore.getRefreshToken()) {
        return throwError(() => error);
      }

      // Access token was rejected — mint a new one and replay the request exactly once.
      return authStore.refreshAccessToken().pipe(
        switchMap((newToken) => {
          if (!newToken) {
            // Refresh failed; the store has already cleared the session and routed to /login.
            return throwError(() => error);
          }
          return next(authorize(req, newToken));
        }),
      );
    }),
  );
};
