export class ApiError extends Error {
  constructor(
    readonly status: number,
    readonly title: string,
    readonly detail: string | null,
  ) {
    super(detail ?? title ?? `Request failed (${status})`);
    this.name = 'ApiError';
  }
}
