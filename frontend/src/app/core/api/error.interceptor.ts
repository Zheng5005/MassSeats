import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { catchError, throwError } from 'rxjs';

import { ApiError } from './error.model';

interface ProblemDetails {
  status?: number;
  title?: string;
  detail?: string;
}

export const errorInterceptor: HttpInterceptorFn = (req, next) =>
  next(req).pipe(
    catchError((error: HttpErrorResponse) => {
      if (error.status === 0) {
        return throwError(
          () =>
            new ApiError(0, 'Network error', 'Could not reach the API. Is the backend running?'),
        );
      }

      const body = error.error as ProblemDetails | null;
      if (
        body &&
        typeof body === 'object' &&
        ('status' in body || 'title' in body || 'detail' in body)
      ) {
        return throwError(
          () =>
            new ApiError(
              body.status ?? error.status,
              body.title ?? error.statusText,
              body.detail ?? null,
            ),
        );
      }

      return throwError(() => new ApiError(error.status, error.statusText, null));
    }),
  );
