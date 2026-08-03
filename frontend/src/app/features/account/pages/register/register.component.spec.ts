import { TestBed } from '@angular/core/testing';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture } from '@angular/core/testing';
import { NavigationEnd, provideRouter, Router } from '@angular/router';
import { filter } from 'rxjs';

import { errorInterceptor } from '../../../../core/api/error.interceptor';
import { User } from '../../../../shared/models/auth.models';
import { PlaceholderPage } from '../../../shell/placeholder-page/placeholder-page.component';
import { Register } from './register.component';

const user: User = {
  id: 'user-1',
  firstName: 'Ada',
  email: 'ada@example.com',
  createdAt: '2026-01-01T00:00:00Z',
  updatedAt: '2026-01-01T00:00:00Z',
};

describe('Register', () => {
  let fixture: ComponentFixture<Register>;
  let httpMock: HttpTestingController;
  let router: Router;

  beforeEach(async () => {
    localStorage.clear();
    await TestBed.configureTestingModule({
      imports: [Register],
      providers: [
        provideRouter([
          { path: 'register', component: Register },
          { path: 'login', component: PlaceholderPage },
          { path: '', component: PlaceholderPage, pathMatch: 'full' },
        ]),
        provideHttpClient(withInterceptors([errorInterceptor])),
        provideHttpClientTesting(),
      ],
    }).compileComponents();

    router = TestBed.inject(Router);
    httpMock = TestBed.inject(HttpTestingController);

    await router.navigateByUrl('/register');
    fixture = TestBed.createComponent(Register);
    fixture.detectChanges();
  });

  afterEach(() => {
    httpMock.verify();
    localStorage.clear();
  });

  function fillForm(): void {
    fixture.componentInstance.form.setValue({
      firstName: 'Ada',
      lastName: 'Lovelace',
      email: 'ada@example.com',
      password: 'pass123',
      nationalId: '',
      phone: '',
    });
    fixture.detectChanges();
  }

  function submit(): void {
    (fixture.nativeElement as HTMLElement)
      .querySelector<HTMLButtonElement>('button[type="submit"]')!
      .click();
  }

  function text(): string {
    return (fixture.nativeElement as HTMLElement).textContent ?? '';
  }

  it('posts the form payload and navigates to /login on success', async () => {
    fillForm();
    const navigationDone = new Promise<void>((resolve) => {
      router.events
        .pipe(filter((event) => event instanceof NavigationEnd && event.url === '/login'))
        .subscribe(() => resolve());
    });

    submit();
    const req = httpMock.expectOne('http://localhost:8080/users');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({
      firstName: 'Ada',
      lastName: 'Lovelace',
      email: 'ada@example.com',
      password: 'pass123',
      nationalId: null,
      phone: null,
    });
    req.flush(user);

    await navigationDone;
    expect(router.url).toBe('/login');
  });

  it('shows the duplicate email error and stays on the page on a 409', async () => {
    fillForm();

    submit();
    httpMock.expectOne('http://localhost:8080/users').flush(
      {
        status: 409,
        title: 'Conflict',
        detail: 'A user with email ada@example.com already exists.',
      },
      { status: 409, statusText: 'Conflict' },
    );
    fixture.detectChanges();

    expect(text()).toContain('A user with email ada@example.com already exists.');
    expect(router.url).toBe('/register');
  });
});
