import { InjectionToken } from '@angular/core';

/**
 * Base URL of the MassSeats gateway API.
 *
 * Defaults to the local gateway; deployments can override it by providing the
 * token in `app.config.ts`, e.g.
 * `provide(API_BASE_URL, useValue: 'https://api.example.com')`, or by setting
 * `globalThis.API_BASE_URL` before bootstrap.
 */
export const API_BASE_URL = new InjectionToken<string>('API_BASE_URL', {
  providedIn: 'root',
  factory: () =>
    (globalThis as unknown as { API_BASE_URL?: string }).API_BASE_URL ?? 'http://localhost:8080',
});
