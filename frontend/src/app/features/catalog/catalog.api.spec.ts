import { TestBed } from '@angular/core/testing';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';

import { errorInterceptor } from '../../core/api/error.interceptor';
import { ApiError } from '../../core/api/error.model';
import { Category, Event, Venue } from '../../shared/models/catalog.models';
import { CatalogService } from './catalog.api';

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

const category: Category = {
  id: 'cat-1',
  name: 'Classical',
  description: 'Orchestral and chamber music',
  createdAt: '2026-01-01T00:00:00Z',
  updatedAt: '2026-01-01T00:00:00Z',
};

describe('CatalogService', () => {
  let service: CatalogService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([errorInterceptor])),
        provideHttpClientTesting(),
      ],
    });
    service = TestBed.inject(CatalogService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('lists events', () => {
    let result: Event[] | undefined;
    service.listEvents().subscribe((events) => (result = events));

    const req = httpMock.expectOne('http://localhost:8080/events');
    expect(req.request.method).toBe('GET');
    req.flush([event]);

    expect(result).toEqual([event]);
  });

  it('gets a single event by id', () => {
    let result: Event | undefined;
    service.getEvent('evt-1').subscribe((e) => (result = e));

    const req = httpMock.expectOne('http://localhost:8080/events/evt-1');
    expect(req.request.method).toBe('GET');
    req.flush(event);

    expect(result).toEqual(event);
  });

  it('lists venues', () => {
    let result: Venue[] | undefined;
    service.listVenues().subscribe((venues) => (result = venues));

    const req = httpMock.expectOne('http://localhost:8080/venues');
    expect(req.request.method).toBe('GET');
    req.flush([venue]);

    expect(result).toEqual([venue]);
  });

  it('gets a single venue by id', () => {
    let result: Venue | undefined;
    service.getVenue('ven-1').subscribe((v) => (result = v));

    const req = httpMock.expectOne('http://localhost:8080/venues/ven-1');
    expect(req.request.method).toBe('GET');
    req.flush(venue);

    expect(result).toEqual(venue);
  });

  it('lists categories', () => {
    let result: Category[] | undefined;
    service.listCategories().subscribe((categories) => (result = categories));

    const req = httpMock.expectOne('http://localhost:8080/categories');
    expect(req.request.method).toBe('GET');
    req.flush([category]);

    expect(result).toEqual([category]);
  });

  it('surfaces backend errors as ApiError', () => {
    let error: unknown;
    service.getEvent('missing').subscribe({ error: (e) => (error = e) });

    httpMock
      .expectOne('http://localhost:8080/events/missing')
      .flush(
        { status: 404, title: 'Not Found', detail: 'Event not found.' },
        { status: 404, statusText: 'Not Found' },
      );

    expect(error).toBeInstanceOf(ApiError);
    const apiError = error as ApiError;
    expect(apiError.status).toBe(404);
    expect(apiError.detail).toBe('Event not found.');
  });
});
