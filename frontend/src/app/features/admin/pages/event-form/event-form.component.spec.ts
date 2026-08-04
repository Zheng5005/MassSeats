import { TestBed } from '@angular/core/testing';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture } from '@angular/core/testing';
import { Component } from '@angular/core';
import { By } from '@angular/platform-browser';
import { NavigationEnd, provideRouter, Router, RouterOutlet } from '@angular/router';
import { filter } from 'rxjs';

import { errorInterceptor } from '../../../../core/api/error.interceptor';
import { Category, Event, Venue } from '../../../../shared/models/catalog.models';
import { PlaceholderPage } from '../../../shell/placeholder-page/placeholder-page.component';
import { EventForm } from './event-form.component';

@Component({
  selector: 'app-test-host',
  imports: [RouterOutlet],
  template: '<router-outlet />',
})
class TestHost {}

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

const futureEvent: Event = {
  id: 'evt-1',
  title: 'Symphony in the Park',
  description: 'An evening of classics under the stars.',
  categoryId: 'cat-1',
  venueId: 'ven-1',
  eventDate: new Date(Date.now() + 7 * 86400000).toISOString(),
  ticketPrice: 45,
  totalSeats: 500,
  availableSeats: 210,
  bannerImage: null,
  createdAt: '2026-01-01T00:00:00Z',
  updatedAt: '2026-01-01T00:00:00Z',
};

describe('EventForm', () => {
  let fixture: ComponentFixture<TestHost>;
  let httpMock: HttpTestingController;
  let router: Router;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [TestHost],
      providers: [
        provideRouter([
          { path: 'admin/events/new', component: EventForm },
          { path: 'admin/events/:id', component: EventForm },
          { path: 'admin/events', component: PlaceholderPage },
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

  function component(): EventForm {
    return fixture.debugElement.query(By.directive(EventForm)).componentInstance;
  }

  function nativeElement(): HTMLElement {
    return fixture.nativeElement as HTMLElement;
  }

  function flushLookups(): void {
    httpMock.expectOne('http://localhost:8080/venues').flush(venues);
    httpMock.expectOne('http://localhost:8080/categories').flush(categories);
    fixture.detectChanges();
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

  function text(): string {
    return nativeElement().textContent ?? '';
  }

  function waitForNavigation(expected: string): Promise<void> {
    return new Promise<void>((resolve) => {
      router.events
        .pipe(filter((event) => event instanceof NavigationEnd && event.url === expected))
        .subscribe(() => resolve());
    });
  }

  function validCreateValues(): {
    title: string;
    description: string;
    categoryId: string;
    venueId: string;
    eventDate: string;
    ticketPrice: number;
    totalSeats: number;
    bannerImage: string;
  } {
    return {
      title: 'New show',
      description: 'A brand new show.',
      categoryId: 'cat-1',
      venueId: 'ven-1',
      eventDate: '2099-01-01T10:00',
      ticketPrice: 25,
      totalSeats: 100,
      bannerImage: 'https://example.com/banner.jpg',
    };
  }

  it('creates an event: POSTs /events with an ISO date and navigates to /admin/events', async () => {
    await createAt('/admin/events/new');
    flushLookups();
    component().form.setValue(validCreateValues());
    fixture.detectChanges();

    const navigationDone = waitForNavigation('/admin/events');
    clickButton('Save event');

    const req = httpMock.expectOne('http://localhost:8080/events');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({
      title: 'New show',
      description: 'A brand new show.',
      categoryId: 'cat-1',
      venueId: 'ven-1',
      eventDate: new Date('2099-01-01T10:00').toISOString(),
      ticketPrice: 25,
      totalSeats: 100,
      bannerImage: 'https://example.com/banner.jpg',
    });
    req.flush(futureEvent);

    await navigationDone;
    expect(router.url).toBe('/admin/events');
  });

  it('blocks submit and shows field errors when required fields are missing', async () => {
    await createAt('/admin/events/new');
    flushLookups();
    const values = validCreateValues();
    values.title = '';
    component().form.setValue(values);
    component().form.controls.title.markAsTouched();
    fixture.detectChanges();

    expect(component().form.invalid).toBe(true);
    const submit = Array.from(nativeElement().querySelectorAll('button')).find(
      (button) => (button.textContent ?? '').trim() === 'Save event',
    );
    expect(submit?.disabled).toBe(true);

    expect(text()).toContain('Title is required');
    expect(router.url).toBe('/admin/events/new');
  });

  it('rejects negative ticket price and zero total seats', async () => {
    await createAt('/admin/events/new');
    flushLookups();
    const values = validCreateValues();
    values.ticketPrice = -5;
    values.totalSeats = 0;
    component().form.setValue(values);
    component().form.controls.ticketPrice.markAsTouched();
    component().form.controls.totalSeats.markAsTouched();
    fixture.detectChanges();

    expect(component().form.controls.ticketPrice.invalid).toBe(true);
    expect(component().form.controls.totalSeats.invalid).toBe(true);
    expect(text()).toContain('Ticket price cannot be negative');
    expect(text()).toContain('Total seats must be at least 1');
    expect(router.url).toBe('/admin/events/new');
  });

  it('edit mode loads the event and PUTs UpdateEventRequest without totalSeats', async () => {
    await createAt('/admin/events/evt-1');
    httpMock.expectOne('http://localhost:8080/events/evt-1').flush(futureEvent);
    flushLookups();

    expect(component().form.controls.title.value).toBe('Symphony in the Park');
    expect(component().form.controls.totalSeats.value).toBe(500);

    component().form.controls.title.setValue('Renamed show');
    fixture.detectChanges();

    const navigationDone = waitForNavigation('/admin/events');
    clickButton('Save event');

    const req = httpMock.expectOne('http://localhost:8080/events/evt-1');
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toEqual({
      title: 'Renamed show',
      description: 'An evening of classics under the stars.',
      categoryId: 'cat-1',
      venueId: 'ven-1',
      eventDate: new Date(component().form.controls.eventDate.value ?? '').toISOString(),
    });
    req.flush({ ...futureEvent, title: 'Renamed show' });

    await navigationDone;
    expect(router.url).toBe('/admin/events');
  });

  it('edit mode saves the price through the separate pricing endpoint', async () => {
    await createAt('/admin/events/evt-1');
    httpMock.expectOne('http://localhost:8080/events/evt-1').flush(futureEvent);
    flushLookups();

    expect(component().pricingPrice.value).toBe(45);
    component().pricingPrice.setValue(60);
    fixture.detectChanges();

    clickButton('Save price');

    const req = httpMock.expectOne('http://localhost:8080/events/evt-1/pricing');
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toEqual({ ticketPrice: 60 });
    req.flush({ ...futureEvent, ticketPrice: 60 });
    fixture.detectChanges();

    expect(text()).toContain('Price updated.');
    expect(router.url).toBe('/admin/events/evt-1');
  });
});
