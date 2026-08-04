import { TestBed } from '@angular/core/testing';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture } from '@angular/core/testing';
import { Component } from '@angular/core';
import { By } from '@angular/platform-browser';
import { NavigationEnd, provideRouter, Router, RouterOutlet } from '@angular/router';
import { filter } from 'rxjs';

import { errorInterceptor } from '../../../../core/api/error.interceptor';
import { Event } from '../../../../shared/models/catalog.models';
import { Reservation } from '../../../../shared/models/booking.models';
import { PlaceholderPage } from '../../../shell/placeholder-page/placeholder-page.component';
import { ReservationCreate } from './reservation-create.component';

@Component({
  selector: 'app-test-host',
  imports: [RouterOutlet],
  template: '<router-outlet />',
})
class TestHost {}

const event: Event = {
  id: 'evt-1',
  title: 'Symphony in the Park',
  description: 'An evening of classics under the stars.',
  categoryId: 'cat-1',
  venueId: 'ven-1',
  eventDate: '2026-09-12T19:30:00Z',
  ticketPrice: 45,
  totalSeats: 500,
  availableSeats: 210,
  bannerImage: null,
  createdAt: '2026-01-01T00:00:00Z',
  updatedAt: '2026-01-01T00:00:00Z',
};

const createdReservation: Reservation = {
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
};

describe('ReservationCreate', () => {
  let fixture: ComponentFixture<TestHost>;
  let httpMock: HttpTestingController;
  let router: Router;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [TestHost],
      providers: [
        provideRouter([
          { path: 'events/:id/book', component: ReservationCreate },
          { path: 'events/:id', component: PlaceholderPage },
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

  afterEach(() => httpMock.verify());

  async function createAt(url: string): Promise<void> {
    await router.navigateByUrl(url);
    fixture.detectChanges();
  }

  function component(): ReservationCreate {
    return fixture.debugElement.query(By.directive(ReservationCreate)).componentInstance;
  }

  function nativeElement(): HTMLElement {
    return fixture.nativeElement as HTMLElement;
  }

  function text(): string {
    return nativeElement().textContent ?? '';
  }

  function clickButton(label: string): void {
    const buttons = nativeElement().querySelectorAll('button');
    for (const button of Array.from(buttons)) {
      if ((button.textContent ?? '').trim() === label) {
        button.click();
        return;
      }
    }
    throw new Error(`No button with label "${label}" found`);
  }

  function waitForNavigation(expected: string): Promise<void> {
    return new Promise<void>((resolve) => {
      router.events
        .pipe(filter((event) => event instanceof NavigationEnd && event.url === expected))
        .subscribe(() => resolve());
    });
  }

  it('prefills the price and creates a reservation via POST /booking/reservations', async () => {
    await createAt('/events/evt-1/book');
    httpMock.expectOne('http://localhost:8080/events/evt-1').flush(event);
    fixture.detectChanges();

    expect(text()).toContain('Symphony in the Park');
    expect(text()).toContain('$45.00');

    component().form.patchValue({
      seatSection: 'Orchestra',
      seatRow: 'A',
      seatNumber: 12,
    });
    fixture.detectChanges();

    const navigationDone = waitForNavigation('/reservations/res-1');
    clickButton('Book seat');

    const req = httpMock.expectOne('http://localhost:8080/booking/reservations');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({
      eventId: 'evt-1',
      seatSection: 'Orchestra',
      seatRow: 'A',
      seatNumber: 12,
      price: 45,
    });
    req.flush(createdReservation);

    await navigationDone;
    expect(router.url).toBe('/reservations/res-1');
  });

  it('shows a not-found state for a missing event', async () => {
    await createAt('/events/missing/book');
    httpMock
      .expectOne('http://localhost:8080/events/missing')
      .flush(
        { status: 404, title: 'Not Found', detail: 'Event not found.' },
        { status: 404, statusText: 'Not Found' },
      );
    fixture.detectChanges();

    expect(text()).toContain('Event not found');
  });

  it('blocks submit and shows field errors when seat details are missing', async () => {
    await createAt('/events/evt-1/book');
    httpMock.expectOne('http://localhost:8080/events/evt-1').flush(event);
    fixture.detectChanges();

    const submit = Array.from(nativeElement().querySelectorAll('button')).find(
      (button) => (button.textContent ?? '').trim() === 'Book seat',
    );
    expect(submit?.disabled).toBe(true);

    component().form.markAllAsTouched();
    fixture.detectChanges();

    expect(text()).toContain('Seat section is required');
    expect(text()).toContain('Seat row is required');
    expect(text()).toContain('Seat number is required');
    expect(router.url).toBe('/events/evt-1/book');
  });

  it('shows a readable error when the event fails to load', async () => {
    await createAt('/events/evt-1/book');
    httpMock
      .expectOne('http://localhost:8080/events/evt-1')
      .flush(
        { title: 'Unauthorized', status: 401, detail: 'Valid JWT token is required.' },
        { status: 401, statusText: 'Unauthorized' },
      );
    fixture.detectChanges();

    expect(text()).toContain("Couldn't load this event");
    expect(text()).toContain('Valid JWT token is required.');
  });
});
