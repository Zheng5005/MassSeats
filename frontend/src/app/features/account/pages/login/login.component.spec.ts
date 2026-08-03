import { TestBed } from '@angular/core/testing';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture } from '@angular/core/testing';
import { NavigationEnd, provideRouter, Router } from '@angular/router';
import { filter } from 'rxjs';

import { errorInterceptor } from '../../../../core/api/error.interceptor';
import { AuthService } from '../../../../core/auth/auth.service';
import { User } from '../../../../shared/models/auth.models';
import { PlaceholderPage } from '../../../shell/placeholder-page/placeholder-page.component';
import { Login } from './login.component';

const user: User = {
  id: 'user-1',
  firstName: 'Ada',
  email: 'ada@example.com',
  createdAt: '2026-01-01T00:00:00Z',
  updatedAt: '2026-01-01T00:00:00Z',
};

describe('Login', () => {
  let fixture: ComponentFixture<Login>;
  let httpMock: HttpTestingController;
  let router: Router;
  let auth: AuthService;

  beforeEach(async () => {
    localStorage.clear();
    await TestBed.configureTestingModule({
      imports: [Login],
      providers: [
        provideRouter([
          { path: 'login', component: Login },
          { path: 'register', component: PlaceholderPage },
          { path: 'events', component: PlaceholderPage },
          { path: '', component: PlaceholderPage, pathMatch: 'full' },
        ]),
        provideHttpClient(withInterceptors([errorInterceptor])),
        provideHttpClientTesting(),
      ],
    }).compileComponents();

    router = TestBed.inject(Router);
    auth = TestBed.inject(AuthService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
    localStorage.clear();
  });

  async function createAt(url: string): Promise<void> {
    await router.navigateByUrl(url);
    fixture = TestBed.createComponent(Login);
    fixture.detectChanges();
  }

  function submit(email: string, password: string): void {
    fixture.componentInstance.form.setValue({ email, password });
    fixture.detectChanges();
    (fixture.nativeElement as HTMLElement)
      .querySelector<HTMLButtonElement>('button[type="submit"]')!
      .click();
  }

  function text(): string {
    return (fixture.nativeElement as HTMLElement).textContent ?? '';
  }

  function waitForNavigation(expected: string): Promise<void> {
    return new Promise<void>((resolve) => {
      router.events
        .pipe(filter((event) => event instanceof NavigationEnd && event.url === expected))
        .subscribe(() => resolve());
    });
  }

  it('stores token and user and navigates to the returnUrl from query params', async () => {
    await createAt('/login?returnUrl=/events');
    const navigationDone = waitForNavigation('/events');

    submit('ada@example.com', 'pass123');
    httpMock.expectOne('http://localhost:8080/users/login').flush({ token: 'jwt-token', user });

    await navigationDone;

    expect(router.url).toBe('/events');
    expect(auth.token()).toBe('jwt-token');
    expect(auth.user()).toEqual(user);
    expect(auth.isAuthenticated()).toBe(true);
  });

  it('navigates to the root route when no returnUrl is present', async () => {
    await createAt('/login');
    const navigationDone = waitForNavigation('/');

    submit('ada@example.com', 'pass123');
    httpMock.expectOne('http://localhost:8080/users/login').flush({ token: 'jwt-token', user });

    await navigationDone;

    expect(router.url).toBe('/');
  });

  it('shows the error message and does not navigate on failed login', async () => {
    await createAt('/login');
    fixture.componentInstance.form.setValue({ email: 'ada@example.com', password: 'wrong' });
    fixture.detectChanges();
    (fixture.nativeElement as HTMLElement)
      .querySelector<HTMLButtonElement>('button[type="submit"]')!
      .click();

    httpMock
      .expectOne('http://localhost:8080/users/login')
      .flush(
        { status: 401, title: 'Unauthorized', detail: 'Invalid email or password.' },
        { status: 401, statusText: 'Unauthorized' },
      );
    fixture.detectChanges();

    expect(text()).toContain('Invalid email or password.');
    expect(router.url).toBe('/login');
    expect(auth.token()).toBeNull();
  });
});
