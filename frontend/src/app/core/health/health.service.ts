import { inject, Injectable, PLATFORM_ID, signal } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { catchError, map, of } from 'rxjs';

import { API_BASE_URL } from '../api/api.config';

type HealthStatus = 'checking' | 'online' | 'offline';

@Injectable({ providedIn: 'root' })
export class HealthService {
  readonly status = signal<HealthStatus>('checking');

  private readonly platformId = inject(PLATFORM_ID);
  private readonly http = inject(HttpClient);
  private readonly apiBase = inject(API_BASE_URL);

  check(): void {
    if (!isPlatformBrowser(this.platformId)) {
      return;
    }

    this.http
      .get(`${this.apiBase}/health`, { responseType: 'json' })
      .pipe(
        map((): HealthStatus => 'online'),
        catchError((error: unknown) => {
          const status = (error as { status?: number }).status ?? 0;
          return of(status === 401 ? ('online' as HealthStatus) : 'offline');
        }),
      )
      .subscribe((status) => this.status.set(status));
  }
}
