import { TestBed } from '@angular/core/testing';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';

import { errorInterceptor } from '../../core/api/error.interceptor';
import { Event, Venue } from '../../shared/models/catalog.models';
import { AdminService } from './admin.api';

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

const venue: Venue = {
  id: 'ven-1',
  name: 'Riverside Amphitheatre',
  address: '1 Park Lane',
  city: 'Springfield',
  country: 'USA',
  capacity: 500,
  createdAt: '2026-01-01T00:00:00Z',
  updatedAt: '2026-01-01T00:00:00Z',
};

describe('AdminService', () => {
  let service: AdminService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([errorInterceptor])),
        provideHttpClientTesting(),
      ],
    });
    service = TestBed.inject(AdminService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('creates an event with POST /events', () => {
    let result: Event | undefined;
    const body = {
      title: 'New show',
      description: null,
      categoryId: 'cat-1',
      venueId: 'ven-1',
      eventDate: '2026-10-01T20:00:00.000Z',
      ticketPrice: 25,
      totalSeats: 100,
      bannerImage: null,
    };
    service.createEvent(body).subscribe((e) => (result = e));

    const req = httpMock.expectOne('http://localhost:8080/events');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(body);
    req.flush(event);

    expect(result).toEqual(event);
  });

  it('updates an event with PUT /events/:id', () => {
    let result: Event | undefined;
    const body = {
      title: 'Updated title',
      description: null,
      categoryId: 'cat-1',
      venueId: 'ven-1',
      eventDate: '2026-10-01T20:00:00.000Z',
    };
    service.updateEvent('evt-1', body).subscribe((e) => (result = e));

    const req = httpMock.expectOne('http://localhost:8080/events/evt-1');
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toEqual(body);
    req.flush({ ...event, title: 'Updated title' });

    expect(result?.title).toBe('Updated title');
  });

  it('updates event pricing with PUT /events/:id/pricing', () => {
    let result: Event | undefined;
    service.updateEventPricing('evt-1', { ticketPrice: 60 }).subscribe((e) => (result = e));

    const req = httpMock.expectOne('http://localhost:8080/events/evt-1/pricing');
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toEqual({ ticketPrice: 60 });
    req.flush({ ...event, ticketPrice: 60 });

    expect(result?.ticketPrice).toBe(60);
  });

  it('deletes events and venues with DELETE to the right URL', () => {
    let eventCompleted = false;
    service.deleteEvent('evt-1').subscribe(() => (eventCompleted = true));

    let req = httpMock.expectOne('http://localhost:8080/events/evt-1');
    expect(req.request.method).toBe('DELETE');
    req.flush(null);
    expect(eventCompleted).toBe(true);

    let venueCompleted = false;
    service.deleteVenue('ven-1').subscribe(() => (venueCompleted = true));

    req = httpMock.expectOne('http://localhost:8080/venues/ven-1');
    expect(req.request.method).toBe('DELETE');
    req.flush(null);
    expect(venueCompleted).toBe(true);
  });

  it('creates a venue with POST /venues', () => {
    let result: Venue | undefined;
    const body = {
      name: 'New Hall',
      address: '2 Park Lane',
      city: 'Springfield',
      country: 'USA',
      capacity: 300,
    };
    service.createVenue(body).subscribe((v) => (result = v));

    const req = httpMock.expectOne('http://localhost:8080/venues');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(body);
    req.flush(venue);

    expect(result).toEqual(venue);
  });

  it('updates a venue with PUT /venues/:id', () => {
    let result: Venue | undefined;
    const body = {
      name: 'Renamed Hall',
      address: '2 Park Lane',
      city: 'Springfield',
      country: 'USA',
      capacity: 400,
    };
    service.updateVenue('ven-1', body).subscribe((v) => (result = v));

    const req = httpMock.expectOne('http://localhost:8080/venues/ven-1');
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toEqual(body);
    req.flush({ ...venue, name: 'Renamed Hall', capacity: 400 });

    expect(result?.name).toBe('Renamed Hall');
  });
});
