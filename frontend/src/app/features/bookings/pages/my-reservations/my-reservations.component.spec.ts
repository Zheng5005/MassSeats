import { TestBed } from '@angular/core/testing';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture } from '@angular/core/testing';
import { Component } from '@angular/core';
import { By } from '@angular/platform-browser';
import { provideRouter, Router, RouterOutlet } from '@angular/router';

import { errorInterceptor } from '../../../../core/api/error.interceptor';
import { Reservation } from '../../../../shared/models/booking.models';
import { PlaceholderPage } from '../../../shell/placeholder-page/placeholder-page.component';
import { MyReservations } from './my-reservations.component';

@Component({
  selector: 'app-test-host',
  imports: [RouterOutlet],
  template: '<router-outlet />',
})
class TestHost {}

const reservations: Reservation[] = [
  {
    id: 'res-1',
    userId: 'user-1',
    eventId: 'evt-1',
    seatSection: 'Orchestra',
    seatRow: 'A',
    seatNumber: 12,
    price: 45,
    status: 'Pending',
    reservedAt: '2026-09-12T10:00:00Z',
    expiresAt: '2026-09-12T10:05:00Z',
  },
  {
    id: 'res-2',
    userId: 'user-1',
    eventId: 'evt-2',
    seatSection: 'Mezzanine',
    seatRow: 'B',
    seatNumber: 3,
    price: 30,
    status: 'Confirmed',
    reservedAt: '2026-09-13T14:00:00Z',
    expiresAt: '2026-09-13T14:05:00Z',
  },
];

describe('MyReservations', () => {
  let fixture: ComponentFixture<TestHost>;
  let httpMock: HttpTestingController;
  let router: Router;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [TestHost],
      providers: [
        provideRouter([
          { path: 'reservations', component: MyReservations },
          { path: 'reservations/:id', component: PlaceholderPage },
          { path: 'events', component: PlaceholderPage },
        ]),
        provideHttpClient(withInterceptors([errorInterceptor])),
        provideHttpClientTesting(),
      ],
    }).compileComponents();

    router = TestBed.inject(Router);
    httpMock = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(TestHost);
    fixture.detectChanges();
  });

  afterEach(() => {
    fixture.destroy();
    httpMock.verify();
  });

  async function createAt(url: string): Promise<void> {
    await router.navigateByUrl(url);
    fixture.detectChanges();
  }

  function component(): MyReservations {
    return fixture.debugElement.query(By.directive(MyReservations)).componentInstance;
  }

  function nativeElement(): HTMLElement {
    return fixture.nativeElement as HTMLElement;
  }

  function text(): string {
    return nativeElement().textContent ?? '';
  }

  function anchorWithText(label: string): HTMLAnchorElement | undefined {
    return Array.from(nativeElement().querySelectorAll<HTMLAnchorElement>('a')).find(
      (anchor) => (anchor.textContent ?? '').trim() === label,
    );
  }

  it('renders the reservations returned by the API', async () => {
    await createAt('/reservations');
    httpMock.expectOne('http://localhost:8080/booking/reservations').flush(reservations);
    fixture.detectChanges();

    expect(text()).toContain('My reservations');
    expect(text()).toContain('Orchestra — Row A, Seat 12');
    expect(text()).toContain('$45.00');
    expect(text()).toContain('Mezzanine — Row B, Seat 3');
    expect(text()).toContain('$30.00');
    expect(text()).toContain('Pending');
    expect(text()).toContain('Confirmed');
    expect(anchorWithText('View details')).toBeDefined();
  });

  it('shows the empty state when there are no reservations', async () => {
    await createAt('/reservations');
    httpMock.expectOne('http://localhost:8080/booking/reservations').flush([]);
    fixture.detectChanges();

    expect(text()).toContain("You don't have any reservations yet");
    expect(anchorWithText('Browse events')).toBeDefined();
  });

  it('shows a readable error and retries the request', async () => {
    await createAt('/reservations');
    httpMock
      .expectOne('http://localhost:8080/booking/reservations')
      .flush(
        { title: 'Unauthorized', status: 401, detail: 'Valid JWT token is required.' },
        { status: 401, statusText: 'Unauthorized' },
      );
    fixture.detectChanges();

    expect(text()).toContain("Couldn't load your reservations");
    expect(text()).toContain('Valid JWT token is required.');

    const retry = Array.from(nativeElement().querySelectorAll('button')).find(
      (button) => (button.textContent ?? '').trim() === 'Try again',
    );
    expect(retry).toBeDefined();
    retry!.click();

    const req = httpMock.expectOne('http://localhost:8080/booking/reservations');
    expect(req.request.method).toBe('GET');
    req.flush(reservations);
    fixture.detectChanges();

    expect(component().reservations()).toHaveLength(2);
    expect(text()).toContain('Orchestra — Row A, Seat 12');
  });
});
