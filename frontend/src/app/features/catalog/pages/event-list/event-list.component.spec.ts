import { TestBed } from '@angular/core/testing';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture } from '@angular/core/testing';
import { NavigationEnd, provideRouter, Router } from '@angular/router';
import { filter } from 'rxjs';

import { errorInterceptor } from '../../../../core/api/error.interceptor';
import { PlaceholderPage } from '../../../../features/shell/placeholder-page/placeholder-page.component';
import { Category, Event, Venue } from '../../../../shared/models/catalog.models';
import { EventList } from './event-list.component';

const events: Event[] = [
  {
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
  },
];

const venues: Venue[] = [
  {
    id: 'ven-1',
    name: 'Riverside Amphitheatre',
    address: '1 Park Lane',
    city: 'Springfield',
    country: 'USA',
    capacity: 500,
    createdAt: '2026-01-01T00:00:00Z',
    updatedAt: '2026-01-01T00:00:00Z',
  },
];

const categories: Category[] = [
  {
    id: 'cat-1',
    name: 'Classical',
    description: 'Orchestral and chamber music',
    createdAt: '2026-01-01T00:00:00Z',
    updatedAt: '2026-01-01T00:00:00Z',
  },
];

describe('EventList', () => {
  let fixture: ComponentFixture<EventList>;
  let httpMock: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [EventList],
      providers: [
        provideRouter([{ path: 'events/:id', component: PlaceholderPage }]),
        provideHttpClient(withInterceptors([errorInterceptor])),
        provideHttpClientTesting(),
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(EventList);
    httpMock = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
  });

  afterEach(() => httpMock.verify());

  function flushCatalog(eventData: Event[], venueData: Venue[], categoryData: Category[]): void {
    httpMock.expectOne('http://localhost:8080/events').flush(eventData);
    httpMock.expectOne('http://localhost:8080/venues').flush(venueData);
    httpMock.expectOne('http://localhost:8080/categories').flush(categoryData);
    fixture.detectChanges();
  }

  function text(): string {
    return (fixture.nativeElement as HTMLElement).textContent ?? '';
  }

  it('renders events with resolved venue name, category chip and price', () => {
    flushCatalog(events, venues, categories);

    expect(text()).toContain('Symphony in the Park');
    expect(text()).toContain('Riverside Amphitheatre');
    expect(text()).toContain('Classical');
    expect(text()).toContain('$45.00');
    expect(text()).toContain('210 / 500 seats available');
  });

  it('shows the empty state when there are no events', () => {
    flushCatalog([], venues, categories);

    expect(text()).toContain('No events yet');
    expect(text()).toContain('Check back soon');
  });

  it('shows a readable error state when loading fails', () => {
    httpMock.expectOne('http://localhost:8080/venues').flush([]);
    httpMock.expectOne('http://localhost:8080/categories').flush([]);
    httpMock
      .expectOne('http://localhost:8080/events')
      .flush(
        { title: 'Unauthorized', status: 401, detail: 'Valid JWT token is required.' },
        { status: 401, statusText: 'Unauthorized' },
      );
    fixture.detectChanges();

    expect(text()).toContain("Couldn't load events");
    expect(text()).toContain('Valid JWT token is required.');
  });

  it('navigates to the event detail page when a card is clicked', async () => {
    flushCatalog(events, venues, categories);

    const router = TestBed.inject(Router);
    const navigationDone = new Promise<void>((resolve) => {
      router.events
        .pipe(filter((event) => event instanceof NavigationEnd))
        .subscribe(() => resolve());
    });

    const link = fixture.nativeElement.querySelector(
      'a[href="/events/evt-1"]',
    ) as HTMLAnchorElement;
    expect(link).not.toBeNull();
    link.click();

    await navigationDone;
    expect(router.url).toBe('/events/evt-1');
  });
});
