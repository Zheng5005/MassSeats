import { TestBed } from '@angular/core/testing';
import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';

import { ApiError } from './error.model';
import { errorInterceptor } from './error.interceptor';

describe('errorInterceptor', () => {
  let http: HttpClient;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([errorInterceptor])),
        provideHttpClientTesting(),
      ],
    });
    http = TestBed.inject(HttpClient);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('converts a problem+json 401 response to ApiError', () => {
    let error: unknown;
    http.get('/api/resource').subscribe({ error: (e) => (error = e) });

    httpMock
      .expectOne('/api/resource')
      .flush(
        { title: 'Unauthorized', status: 401, detail: 'Valid JWT token is required.' },
        { status: 401, statusText: 'Unauthorized' },
      );

    expect(error).toBeInstanceOf(ApiError);
    const apiError = error as ApiError;
    expect(apiError.status).toBe(401);
    expect(apiError.title).toBe('Unauthorized');
    expect(apiError.detail).toBe('Valid JWT token is required.');
  });

  it('maps network errors (status 0) to a friendly ApiError', () => {
    let error: unknown;
    http.get('/api/resource').subscribe({ error: (e) => (error = e) });

    httpMock.expectOne('/api/resource').error(new ProgressEvent('error'), {
      status: 0,
      statusText: 'Unknown Error',
    });

    expect(error).toBeInstanceOf(ApiError);
    const apiError = error as ApiError;
    expect(apiError.status).toBe(0);
    expect(apiError.title).toBe('Network error');
    expect(apiError.detail).toBe('Could not reach the API. Is the backend running?');
  });

  it('falls back to statusText for non-problem error bodies', () => {
    let error: unknown;
    http.get('/api/resource').subscribe({ error: (e) => (error = e) });

    httpMock
      .expectOne('/api/resource')
      .flush({ message: 'boom' }, { status: 500, statusText: 'Server Error' });

    expect(error).toBeInstanceOf(ApiError);
    const apiError = error as ApiError;
    expect(apiError.status).toBe(500);
    expect(apiError.title).toBe('Server Error');
    expect(apiError.detail).toBeNull();
  });
});
