import { TestBed } from '@angular/core/testing';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';

import { errorInterceptor } from '../api/error.interceptor';
import { ApiError } from '../api/error.model';
import { User } from '../../shared/models/auth.models';
import { AuthService } from './auth.service';

const user: User = {
  id: '1',
  firstName: 'Ada',
  email: 'ada@example.com',
  createdAt: '2026-01-01T00:00:00Z',
  updatedAt: '2026-01-01T00:00:00Z',
};

describe('AuthService', () => {
  let service: AuthService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([errorInterceptor])),
        provideHttpClientTesting(),
      ],
    });
    service = TestBed.inject(AuthService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
    localStorage.clear();
  });

  it('stores token and user on successful login', () => {
    let emitted = false;
    service.login('ada@example.com', 'pass123').subscribe(() => (emitted = true));

    const req = httpMock.expectOne('http://localhost:8080/users/login');
    expect(req.request.method).toBe('POST');
    req.flush({ token: 'jwt-token', user });

    expect(emitted).toBe(true);
    expect(service.token()).toBe('jwt-token');
    expect(service.user()).toEqual(user);
    expect(service.isAuthenticated()).toBe(true);
    expect(localStorage.getItem('massseats.token')).toBe('jwt-token');
    expect(localStorage.getItem('massseats.user')).toBe(JSON.stringify(user));
  });

  it('throws ApiError on failed login and leaves signals null', () => {
    let error: unknown;
    service.login('ada@example.com', 'wrong').subscribe({ error: (e) => (error = e) });

    httpMock
      .expectOne('http://localhost:8080/users/login')
      .flush(
        { title: 'Unauthorized', status: 401, detail: 'Valid JWT token is required.' },
        { status: 401, statusText: 'Unauthorized' },
      );

    expect(error).toBeInstanceOf(ApiError);
    const apiError = error as ApiError;
    expect(apiError.status).toBe(401);
    expect(apiError.title).toBe('Unauthorized');
    expect(service.token()).toBeNull();
    expect(service.user()).toBeNull();
    expect(service.isAuthenticated()).toBe(false);
  });

  it('clears signals and storage on logout', () => {
    service.login('ada@example.com', 'pass').subscribe();
    httpMock.expectOne('http://localhost:8080/users/login').flush({ token: 'jwt-token', user });
    expect(service.isAuthenticated()).toBe(true);

    service.logout();

    expect(service.token()).toBeNull();
    expect(service.user()).toBeNull();
    expect(service.isAuthenticated()).toBe(false);
    expect(localStorage.getItem('massseats.token')).toBeNull();
    expect(localStorage.getItem('massseats.user')).toBeNull();
  });
});
