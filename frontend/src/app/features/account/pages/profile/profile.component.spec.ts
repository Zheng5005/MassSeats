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
import { Profile } from './profile.component';

const user: User = {
  id: 'user-1',
  firstName: 'Ada',
  lastName: 'Lovelace',
  email: 'ada@example.com',
  nationalId: '123456789',
  phone: '555-0100',
  createdAt: '2026-01-01T00:00:00Z',
  updatedAt: '2026-01-01T00:00:00Z',
};

const updatedUser: User = {
  ...user,
  firstName: 'Grace',
  lastName: 'Hopper',
  phone: '555-9999',
  profileImage: 'https://example.com/grace.jpg',
  updatedAt: '2026-02-01T00:00:00Z',
};

describe('Profile', () => {
  let fixture: ComponentFixture<Profile>;
  let httpMock: HttpTestingController;
  let router: Router;
  let auth: AuthService;

  beforeEach(async () => {
    localStorage.clear();
    localStorage.setItem('massseats.token', 'jwt-token');
    localStorage.setItem('massseats.user', JSON.stringify(user));
    await TestBed.configureTestingModule({
      imports: [Profile],
      providers: [
        provideRouter([
          { path: 'profile', component: Profile },
          { path: 'login', component: PlaceholderPage },
          { path: '', component: PlaceholderPage, pathMatch: 'full' },
        ]),
        provideHttpClient(withInterceptors([errorInterceptor])),
        provideHttpClientTesting(),
      ],
    }).compileComponents();

    router = TestBed.inject(Router);
    auth = TestBed.inject(AuthService);
    httpMock = TestBed.inject(HttpTestingController);

    await router.navigateByUrl('/profile');
    fixture = TestBed.createComponent(Profile);
    fixture.detectChanges();
  });

  afterEach(() => {
    httpMock.verify();
    localStorage.clear();
  });

  function text(): string {
    return (fixture.nativeElement as HTMLElement).textContent ?? '';
  }

  function buttonWithText(label: string): HTMLButtonElement {
    const buttons = Array.from(
      (fixture.nativeElement as HTMLElement).querySelectorAll<HTMLButtonElement>('button'),
    );
    const button = buttons.find((b) => b.textContent?.trim() === label);
    expect(button).toBeDefined();
    return button!;
  }

  function waitForNavigation(expected: string): Promise<void> {
    return new Promise<void>((resolve) => {
      router.events
        .pipe(filter((event) => event instanceof NavigationEnd && event.url === expected))
        .subscribe(() => resolve());
    });
  }

  it('renders the user details and hydrates the edit form', () => {
    expect(text()).toContain('Ada Lovelace');
    expect(text()).toContain('ada@example.com');
    expect(text()).toContain('123456789');

    const form = fixture.componentInstance.form;
    expect(form.controls.firstName.value).toBe('Ada');
    expect(form.controls.lastName.value).toBe('Lovelace');
    expect(form.controls.phone.value).toBe('555-0100');

    const firstNameInput = fixture.nativeElement.querySelector(
      'input[formcontrolname="firstName"]',
    ) as HTMLInputElement;
    expect(firstNameInput?.value).toBe('Ada');
  });

  it('saves changes with PUT and updates the auth user signal', async () => {
    fixture.componentInstance.form.patchValue({
      firstName: 'Grace',
      lastName: 'Hopper',
      phone: '555-9999',
      profileImage: 'https://example.com/grace.jpg',
    });
    fixture.detectChanges();

    buttonWithText('Save changes').click();
    const req = httpMock.expectOne('http://localhost:8080/users/user-1');
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toEqual({
      firstName: 'Grace',
      lastName: 'Hopper',
      phone: '555-9999',
      profileImage: 'https://example.com/grace.jpg',
    });
    req.flush(updatedUser);
    fixture.detectChanges();

    expect(auth.user()).toEqual(updatedUser);
    expect(text()).toContain('Profile updated.');
  });

  it('deletes the account with a two-step confirm and logs out', async () => {
    buttonWithText('Delete account').click();
    fixture.detectChanges();
    expect(text()).toContain('Are you sure?');

    const navigationDone = waitForNavigation('/');
    buttonWithText('Delete').click();

    const req = httpMock.expectOne('http://localhost:8080/users/user-1');
    expect(req.request.method).toBe('DELETE');
    req.flush(null);

    await navigationDone;
    expect(router.url).toBe('/');
    expect(auth.token()).toBeNull();
    expect(auth.user()).toBeNull();
  });
});
