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

function pad2(value: number): string {
  return String(value).padStart(2, '0');
}

/**
 * Converts an ISO-8601 datetime string into a value for
 * `<input type="datetime-local">` in the browser's local timezone
 * ("YYYY-MM-DDTHH:mm"). Returns an empty string for invalid input.
 */
export function toLocalDateTimeInput(iso: string | null | undefined): string {
  if (!iso) {
    return '';
  }
  const date = new Date(iso);
  if (Number.isNaN(date.getTime())) {
    return '';
  }
  return (
    `${date.getFullYear()}-${pad2(date.getMonth() + 1)}-${pad2(date.getDate())}` +
    `T${pad2(date.getHours())}:${pad2(date.getMinutes())}`
  );
}

/**
 * Converts a `<input type="datetime-local">` value ("YYYY-MM-DDTHH:mm", local
 * time) into ISO-8601 with an offset via toISOString(). The API expects
 * DateTimeOffset, so the local string is parsed as local time and sent as UTC
 * with the `Z` suffix.
 */
export function toIsoDateTime(value: string): string {
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return value;
  }
  return date.toISOString();
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
