import { HttpErrorResponse, HttpInterceptorFn, HttpRequest } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, switchMap, throwError } from 'rxjs';
import { AuthStore } from './auth.store';

function withBearer(req: HttpRequest<unknown>, token: string): HttpRequest<unknown> {
  return req.clone({ setHeaders: { Authorization: `Bearer ${token}` } });
}

/**
 * Attaches the platform JWT to every API request and transparently recovers from access-token
 * expiry: on a 401 it refreshes the token once (concurrent 401s share a single refresh) and replays
 * the original request with the new token. If the refresh itself fails, AuthStore clears the session
 * and redirects to the login page, and the original error is propagated.
 *
 * The auth endpoints (`/api/auth/exchange`, `/api/auth/refresh`) are skipped entirely — they carry no
 * bearer token, and skipping them keeps the refresh call from recursing into this 401 handler.
 * Functional interceptor (Angular 20 pattern).
 */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const authStore = inject(AuthStore);

  if (req.url.includes('/api/auth/')) {
    return next(req);
  }

  const token = authStore.getAccessToken();
  const authReq = token ? withBearer(req, token) : req;

  return next(authReq).pipe(
    catchError((error: unknown) => {
      const isUnauthorized = error instanceof HttpErrorResponse && error.status === 401;
      if (!isUnauthorized || !authStore.getRefreshToken()) {
        return throwError(() => error);
      }

      // Access token was rejected — try to mint a new one and replay the request exactly once.
      return authStore.refreshAccessToken().pipe(
        switchMap((newToken) => {
          if (!newToken) {
            // Refresh failed; the store has already cleared the session and routed to /login.
            return throwError(() => error);
          }
          return next(withBearer(req, newToken));
        }),
      );
    }),
  );
};
