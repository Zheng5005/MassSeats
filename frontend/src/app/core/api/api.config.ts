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

/**
 * Stripe publishable key used to build the in-browser Payment Element checkout.
 *
 * Publishable keys are safe for the browser; the secret key never leaves the
 * backend. Empty string (the default) means in-browser checkout is unavailable
 * and the reservation page falls back to its explanatory copy. Deployments can
 * set `globalThis.STRIPE_PUBLISHABLE_KEY` before bootstrap or provide the token
 * in `app.config.ts`.
 */
export const STRIPE_PUBLISHABLE_KEY = new InjectionToken<string>('STRIPE_PUBLISHABLE_KEY', {
  providedIn: 'root',
  factory: () =>
    (globalThis as unknown as { STRIPE_PUBLISHABLE_KEY?: string }).STRIPE_PUBLISHABLE_KEY ?? '',
});
