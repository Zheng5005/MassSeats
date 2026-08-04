import { TestBed } from '@angular/core/testing';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture } from '@angular/core/testing';
import { provideRouter } from '@angular/router';

import { errorInterceptor } from '../../../../core/api/error.interceptor';
import { Event, Venue } from '../../../../shared/models/catalog.models';
import { PlaceholderPage } from '../../../shell/placeholder-page/placeholder-page.component';
import { EventManage } from './event-manage.component';

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

describe('EventManage', () => {
  let fixture: ComponentFixture<EventManage>;
  let httpMock: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [EventManage],
      providers: [
        provideRouter([
          { path: 'admin/events/new', component: PlaceholderPage },
          { path: 'admin/events/:id', component: PlaceholderPage },
        ]),
        provideHttpClient(withInterceptors([errorInterceptor])),
        provideHttpClientTesting(),
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(EventManage);
    httpMock = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
  });

  afterEach(() => httpMock.verify());

  function flushList(): void {
    httpMock.expectOne('http://localhost:8080/events').flush(events);
    httpMock.expectOne('http://localhost:8080/venues').flush(venues);
    fixture.detectChanges();
  }

  function text(): string {
    return (fixture.nativeElement as HTMLElement).textContent ?? '';
  }

  function rowButtons(): HTMLButtonElement[] {
    const rows = fixture.nativeElement.querySelectorAll('tbody tr');
    return Array.from(rows[0]?.querySelectorAll('button') ?? []);
  }

  it('renders events with resolved venue name, price and an edit link', () => {
    flushList();

    expect(text()).toContain('Symphony in the Park');
    expect(text()).toContain('Riverside Amphitheatre');
    expect(text()).toContain('$45.00');
    expect(fixture.nativeElement.querySelector('a[href="/admin/events/evt-1"]')).not.toBeNull();
  });

  it('deletes an event only after inline confirmation and removes the row', () => {
    flushList();

    rowButtons()
      .find((button) => (button.textContent ?? '').trim() === 'Delete')!
      .click();
    fixture.detectChanges();
    expect(text()).toContain('Are you sure?');

    rowButtons()
      .find((button) => (button.textContent ?? '').trim() === 'Delete')!
      .click();
    const req = httpMock.expectOne('http://localhost:8080/events/evt-1');
    expect(req.request.method).toBe('DELETE');
    req.flush(null);
    fixture.detectChanges();

    expect(text()).not.toContain('Symphony in the Park');
    expect(text()).toContain('No events yet');
  });
});
