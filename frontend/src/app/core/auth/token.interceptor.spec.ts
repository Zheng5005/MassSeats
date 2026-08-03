import { TestBed } from '@angular/core/testing';
import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';

import { PlaceholderPage } from '../../features/shell/placeholder-page/placeholder-page.component';
import { AuthService } from './auth.service';
import { tokenInterceptor } from './token.interceptor';

describe('tokenInterceptor', () => {
  let http: HttpClient;
  let httpMock: HttpTestingController;
  let auth: AuthService;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [
        provideRouter([
          { path: 'login', component: PlaceholderPage, data: { title: 'Login' } },
          { path: '**', component: PlaceholderPage, data: { title: 'Not found' } },
        ]),
        provideHttpClient(withInterceptors([tokenInterceptor])),
        provideHttpClientTesting(),
      ],
    });
    http = TestBed.inject(HttpClient);
    httpMock = TestBed.inject(HttpTestingController);
    auth = TestBed.inject(AuthService);
  });

  afterEach(() => httpMock.verify());

  it('attaches the Authorization header when a token is present', () => {
    auth.token.set('secret-token');
    http.get('/api/data').subscribe();

    const req = httpMock.expectOne('/api/data');
    expect(req.request.headers.get('Authorization')).toBe('Bearer secret-token');
    req.flush({});
  });

  it('does not attach an Authorization header when no token is present', () => {
    http.get('/api/data').subscribe();

    const req = httpMock.expectOne('/api/data');
    expect(req.request.headers.has('Authorization')).toBe(false);
    req.flush({});
  });

  it('logs out on a 401 when the request carried a token', () => {
    auth.token.set('expired-token');
    const logoutSpy = vi.spyOn(auth, 'logout');
    http.get('/api/data').subscribe({ error: () => undefined });

    httpMock.expectOne('/api/data').flush({}, { status: 401, statusText: 'Unauthorized' });

    expect(logoutSpy).toHaveBeenCalled();
    expect(auth.token()).toBeNull();
  });

  it('does not log out on a 401 from the login endpoint', () => {
    auth.token.set('expired-token');
    const logoutSpy = vi.spyOn(auth, 'logout');
    http.post('/users/login', {}).subscribe({ error: () => undefined });

    httpMock.expectOne('/users/login').flush({}, { status: 401, statusText: 'Unauthorized' });

    expect(logoutSpy).not.toHaveBeenCalled();
  });
});
