import { TestBed } from '@angular/core/testing';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture } from '@angular/core/testing';
import { Component } from '@angular/core';
import { By } from '@angular/platform-browser';
import { provideRouter, Router, RouterOutlet } from '@angular/router';

import { errorInterceptor } from '../../../../core/api/error.interceptor';
import { Payment, Reservation } from '../../../../shared/models/booking.models';
import { PlaceholderPage } from '../../../shell/placeholder-page/placeholder-page.component';
import { ReservationDetail } from './reservation-detail.component';

@Component({
  selector: 'app-test-host',
  imports: [RouterOutlet],
  template: '<router-outlet />',
})
class TestHost {}

const reservation: Reservation = {
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

const payment: Payment = {
  id: 'pay-1',
  bookingId: 'res-1',
  stripePaymentIntentId: 'pi_123',
  amount: 45,
  currency: 'USD',
  paymentMethod: 'card',
  status: 'Succeeded',
  createdAt: '2026-09-12T10:01:00Z',
  updatedAt: '2026-09-12T10:02:00Z',
};

describe('ReservationDetail', () => {
  let fixture: ComponentFixture<TestHost>;
  let httpMock: HttpTestingController;
  let router: Router;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [TestHost],
      providers: [
        provideRouter([
          { path: 'reservations/:id', component: ReservationDetail },
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

  function component(): ReservationDetail {
    return fixture.debugElement.query(By.directive(ReservationDetail)).componentInstance;
  }

  function nativeElement(): HTMLElement {
    return fixture.nativeElement as HTMLElement;
  }

  function text(): string {
    return nativeElement().textContent ?? '';
  }

  function buttonWithText(label: string): HTMLButtonElement | undefined {
    return Array.from(nativeElement().querySelectorAll<HTMLButtonElement>('button')).find(
      (button) => (button.textContent ?? '').trim() === label,
    );
  }

  it('renders seat info, price and timestamps for a pending reservation', async () => {
    await createAt('/reservations/res-1');
    httpMock.expectOne('http://localhost:8080/booking/reservations/res-1').flush(reservation);
    fixture.detectChanges();

    expect(text()).toContain('res-1');
    expect(text()).toContain('Orchestra');
    expect(text()).toContain('Row A');
    expect(text()).toContain('Seat 12');
    expect(text()).toContain('$45.00');
    expect(text()).toContain('Pending');
  });

  it('shows the cancel button for a pending reservation and cancels via DELETE', async () => {
    await createAt('/reservations/res-1');
    httpMock.expectOne('http://localhost:8080/booking/reservations/res-1').flush(reservation);
    fixture.detectChanges();

    expect(text()).toContain('Payment pending');
    expect(buttonWithText('Cancel reservation')).toBeDefined();

    buttonWithText('Cancel reservation')!.click();

    const req = httpMock.expectOne('http://localhost:8080/booking/reservations/res-1');
    expect(req.request.method).toBe('DELETE');
    req.flush(null, { status: 204, statusText: 'No Content' });
    fixture.detectChanges();

    // After the 204 the component re-fetches the reservation to reflect the
    // updated status from the backend.
    httpMock
      .expectOne('http://localhost:8080/booking/reservations/res-1')
      .flush({ ...reservation, status: 'Cancelled' });
    fixture.detectChanges();

    expect(text()).toContain('Cancelled');
    expect(buttonWithText('Cancel reservation')).toBeUndefined();
  });

  it('renders payment details for a confirmed reservation', async () => {
    await createAt('/reservations/res-1');
    httpMock
      .expectOne('http://localhost:8080/booking/reservations/res-1')
      .flush({ ...reservation, status: 'Confirmed', paymentId: 'pay-1' });
    httpMock.expectOne('http://localhost:8080/payments/pay-1').flush(payment);
    fixture.detectChanges();

    expect(text()).toContain('Confirmed');
    expect(text()).toContain('payment succeeded');
    expect(text()).toContain('Succeeded');
    expect(text()).toContain('$45.00');
    expect(text()).toContain('USD');
  });

  it('shows a terminal state without a cancel button for a cancelled reservation', async () => {
    await createAt('/reservations/res-1');
    httpMock
      .expectOne('http://localhost:8080/booking/reservations/res-1')
      .flush({ ...reservation, status: 'Cancelled' });
    fixture.detectChanges();

    expect(text()).toContain('Reservation cancelled');
    expect(buttonWithText('Cancel reservation')).toBeUndefined();
  });

  it('shows a terminal state without a cancel button for an expired reservation', async () => {
    await createAt('/reservations/res-1');
    httpMock
      .expectOne('http://localhost:8080/booking/reservations/res-1')
      .flush({ ...reservation, status: 'Expired' });
    fixture.detectChanges();

    expect(text()).toContain('Reservation expired');
    expect(buttonWithText('Cancel reservation')).toBeUndefined();
  });

  it('shows a not-found state when the reservation is missing', async () => {
    await createAt('/reservations/missing');
    httpMock
      .expectOne('http://localhost:8080/booking/reservations/missing')
      .flush(
        { status: 404, title: 'Not Found', detail: 'Reservation not found.' },
        { status: 404, statusText: 'Not Found' },
      );
    fixture.detectChanges();

    expect(text()).toContain('Reservation not found');
  });

  it('shows a readable error when loading fails', async () => {
    await createAt('/reservations/res-1');
    httpMock
      .expectOne('http://localhost:8080/booking/reservations/res-1')
      .flush(
        { title: 'Unauthorized', status: 401, detail: 'Valid JWT token is required.' },
        { status: 401, statusText: 'Unauthorized' },
      );
    fixture.detectChanges();

    expect(text()).toContain("Couldn't load this reservation");
    expect(text()).toContain('Valid JWT token is required.');
  });

  it('refreshes the reservation when the refresh button is clicked', async () => {
    await createAt('/reservations/res-1');
    httpMock.expectOne('http://localhost:8080/booking/reservations/res-1').flush(reservation);
    fixture.detectChanges();

    buttonWithText('Refresh')!.click();

    const req = httpMock.expectOne('http://localhost:8080/booking/reservations/res-1');
    expect(req.request.method).toBe('GET');
    req.flush({ ...reservation, status: 'Confirmed', paymentId: 'pay-1' });
    httpMock.expectOne('http://localhost:8080/payments/pay-1').flush(payment);
    fixture.detectChanges();

    expect(component().reservation()?.status).toBe('Confirmed');
    expect(text()).toContain('payment succeeded');
  });
});
