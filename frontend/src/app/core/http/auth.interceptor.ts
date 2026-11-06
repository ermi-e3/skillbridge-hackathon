import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { BehaviorSubject, catchError, filter, switchMap, take, throwError } from 'rxjs';
import { AuthService } from '../services/auth.service';

let isRefreshing = false;
const refreshTokenSubject = new BehaviorSubject<string | null>(null);

export function resetAuthStateForTesting(): void {
  isRefreshing = false;
  refreshTokenSubject.next(null);
}

function getCookie(name: string): string | null {
  if (typeof document === 'undefined') return null;
  const match = document.cookie.match(new RegExp('(^|;\\s*)(' + name + ')=([^;]*)'));
  return match ? decodeURIComponent(match[3]) : null;
}

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const authService = inject(AuthService);
  const router = inject(Router);

  const isAuthEndpoint =
    req.url.includes('/api/auth/login') ||
    req.url.includes('/api/auth/register') ||
    req.url.includes('/api/auth/refresh') ||
    req.url.includes('/api/auth/logout');

  let headers = req.headers;
  const token = authService.getToken();
  if (token && !isAuthEndpoint) {
    headers = headers.set('Authorization', `Bearer ${token}`);
  }

  // Attach CSRF protection header for mutating requests when XSRF-TOKEN cookie is present
  const xsrfToken = getCookie('XSRF-TOKEN');
  if (xsrfToken && !headers.has('X-XSRF-TOKEN')) {
    headers = headers.set('X-XSRF-TOKEN', xsrfToken);
  }

  const authReq = req.clone({
    headers,
    withCredentials: true,
  });

  return next(authReq).pipe(
    catchError((error: HttpErrorResponse) => {
      if (error.status === 401 && !isAuthEndpoint) {
        if (!isRefreshing) {
          isRefreshing = true;
          refreshTokenSubject.next(null);

          return authService.refreshToken().pipe(
            switchMap((newAuth) => {
              isRefreshing = false;
              refreshTokenSubject.next(newAuth.accessToken);

              const retryReq = req.clone({
                setHeaders: {
                  Authorization: `Bearer ${newAuth.accessToken}`,
                  ...(getCookie('XSRF-TOKEN') ? { 'X-XSRF-TOKEN': getCookie('XSRF-TOKEN')! } : {}),
                },
                withCredentials: true,
              });
              return next(retryReq);
            }),
            catchError((refreshErr) => {
              isRefreshing = false;
              refreshTokenSubject.next(null);
              authService.logout();
              router.navigate(['/login']);
              return throwError(() => refreshErr);
            })
          );
        } else {
          // Wait for concurrent in-flight refresh to complete, then resume with new access token
          return refreshTokenSubject.pipe(
            filter((newToken): newToken is string => newToken !== null),
            take(1),
            switchMap((newToken) => {
              const retryReq = req.clone({
                setHeaders: {
                  Authorization: `Bearer ${newToken}`,
                  ...(getCookie('XSRF-TOKEN') ? { 'X-XSRF-TOKEN': getCookie('XSRF-TOKEN')! } : {}),
                },
                withCredentials: true,
              });
              return next(retryReq);
            })
          );
        }
      }

      return throwError(() => error);
    })
  );
};
