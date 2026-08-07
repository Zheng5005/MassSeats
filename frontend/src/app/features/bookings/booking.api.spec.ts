import { TestBed } from '@angular/core/testing';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';

import { errorInterceptor } from '../../core/api/error.interceptor';
import { CreateReservationRequest, Reservation } from '../../shared/models/booking.models';
import { BookingService } from './booking.api';

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

const request: CreateReservationRequest = {
  eventId: 'evt-1',
  seatSection: 'Orchestra',
  seatRow: 'A',
  seatNumber: 12,
  price: 45,
};

describe('BookingService', () => {
  let service: BookingService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([errorInterceptor])),
        provideHttpClientTesting(),
      ],
    });
    service = TestBed.inject(BookingService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('creates a reservation with POST /booking/reservations', () => {
    let result: Reservation | undefined;
    service.createReservation(request).subscribe((res) => (result = res));

    const req = httpMock.expectOne('http://localhost:8080/booking/reservations');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(request);
    req.flush(reservation);

    expect(result).toEqual(reservation);
  });

  it('gets a single reservation by id', () => {
    let result: Reservation | undefined;
    service.getReservation('res-1').subscribe((res) => (result = res));

    const req = httpMock.expectOne('http://localhost:8080/booking/reservations/res-1');
    expect(req.request.method).toBe('GET');
    req.flush(reservation);

    expect(result).toEqual(reservation);
  });

  it('lists the current user reservations with GET /booking/reservations', () => {
    let result: Reservation[] | undefined;
    service.listReservations().subscribe((res) => (result = res));

    const req = httpMock.expectOne('http://localhost:8080/booking/reservations');
    expect(req.request.method).toBe('GET');
    req.flush([reservation]);

    expect(result).toEqual([reservation]);
  });

  it('cancels a reservation with DELETE and completes without a body', () => {
    let completed = false;
    service.cancelReservation('res-1').subscribe({ complete: () => (completed = true) });

    const req = httpMock.expectOne('http://localhost:8080/booking/reservations/res-1');
    expect(req.request.method).toBe('DELETE');
    req.flush(null, { status: 204, statusText: 'No Content' });

    expect(completed).toBe(true);
  });
});
