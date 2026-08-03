import { inject } from '@angular/core';
import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';

import { AuthService } from './auth.service';

export const tokenInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);
  const router = inject(Router);
  const token = auth.token();
  const isLoginRequest = req.url.includes('/users/login');

  let request = req;
  if (token) {
    request = req.clone({ setHeaders: { Authorization: `Bearer ${token}` } });
  }

  return next(request).pipe(
    catchError((error: HttpErrorResponse) => {
      if (error.status === 401 && token && !isLoginRequest) {
        const currentUrl = router.url;
        auth.logout();
        if (!currentUrl.startsWith('/login')) {
          router.navigateByUrl(
            router.createUrlTree(['/login'], { queryParams: { returnUrl: currentUrl } }),
          );
        }
      }
      return throwError(() => error);
    }),
  );
};
