import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, from, map, switchMap, throwError } from 'rxjs';
import { AuthStore } from './auth-store';

export const authInterceptor: HttpInterceptorFn = (request, next) => {
  if (!request.url.startsWith('/api/') || request.url.startsWith('/api/v1/auth/'))
    return next(request);
  const auth = inject(AuthStore);
  const owner = auth.user()?.id;
  const ensureOwner = () => {
    if (owner && auth.user()?.id !== owner)
      throw new HttpErrorResponse({
        status: 409,
        statusText: 'Account changed',
        error: { title: 'Your account changed. Please try again.' },
      });
  };
  return from(auth.token()).pipe(
    switchMap((token) => {
      ensureOwner();
      return next(
        token ? request.clone({ setHeaders: { Authorization: 'Bearer ' + token } }) : request,
      );
    }),
    map((event) => {
      ensureOwner();
      return event;
    }),
    catchError((error: unknown) => {
      if (!(error instanceof HttpErrorResponse) || error.status !== 401)
        return throwError(() => error);
      ensureOwner();
      return from(auth.refresh()).pipe(
        switchMap((response) => {
          ensureOwner();
          if (owner && response && response.user.id !== owner)
            return throwError(
              () => new HttpErrorResponse({ status: 409, statusText: 'Account changed' }),
            );
          return response
            ? next(
                request.clone({ setHeaders: { Authorization: 'Bearer ' + response.accessToken } }),
              )
            : throwError(() => error);
        }),
        map((event) => {
          ensureOwner();
          return event;
        }),
      );
    }),
  );
};
