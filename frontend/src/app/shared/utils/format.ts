/**
 * Formatting helpers for catalog data. Purely functional and SSR-safe: they
 * never touch the DOM, `window` or `localStorage`, and they always use an
 * explicit locale so the server and browser render identical strings.
 */

const DATE_LOCALE = 'en-US';

export function formatDate(iso: string | null | undefined): string {
  if (!iso) {
    return '—';
  }
  const date = new Date(iso);
  if (Number.isNaN(date.getTime())) {
    return '—';
  }
  return date.toLocaleDateString(DATE_LOCALE, {
    weekday: 'short',
    year: 'numeric',
    month: 'short',
    day: 'numeric',
  });
}

export function formatTime(iso: string | null | undefined): string {
  if (!iso) {
    return '—';
  }
  const date = new Date(iso);
  if (Number.isNaN(date.getTime())) {
    return '—';
  }
  return date.toLocaleTimeString(DATE_LOCALE, {
    hour: 'numeric',
    minute: '2-digit',
  });
}

export function formatPrice(amount: number | null | undefined): string {
  if (amount == null) {
    return '—';
  }
  return new Intl.NumberFormat(DATE_LOCALE, {
    style: 'currency',
    currency: 'USD',
  }).format(amount);
}

/**
 * Turns an unknown thrown error (ApiError, HttpErrorResponse, ...) into a
 * short, readable message. Duck-typed on purpose so this stays dependency-free.
 */
export function errorMessage(error: unknown): string {
  if (typeof error === 'object' && error !== null) {
    const maybe = error as { title?: unknown; detail?: unknown; message?: unknown };
    if (typeof maybe.detail === 'string') {
      return maybe.detail;
    }
    if (typeof maybe.title === 'string') {
      return maybe.title;
    }
    if (typeof maybe.message === 'string') {
      return maybe.message;
    }
  }
  return 'Something went wrong while loading this page.';
}
