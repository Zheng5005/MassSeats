import { TestBed } from '@angular/core/testing';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture } from '@angular/core/testing';
import { Component } from '@angular/core';
import { By } from '@angular/platform-browser';
import { NavigationEnd, provideRouter, Router, RouterOutlet } from '@angular/router';
import { filter } from 'rxjs';

import { errorInterceptor } from '../../../../core/api/error.interceptor';
import { Venue } from '../../../../shared/models/catalog.models';
import { PlaceholderPage } from '../../../shell/placeholder-page/placeholder-page.component';
import { VenueForm } from './venue-form.component';

@Component({
  selector: 'app-test-host',
  imports: [RouterOutlet],
  template: '<router-outlet />',
})
class TestHost {}

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

describe('VenueForm', () => {
  let fixture: ComponentFixture<TestHost>;
  let httpMock: HttpTestingController;
  let router: Router;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [TestHost],
      providers: [
        provideRouter([
          { path: 'admin/venues/new', component: VenueForm },
          { path: 'admin/venues/:id', component: VenueForm },
          { path: 'admin/venues', component: PlaceholderPage },
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

  function component(): VenueForm {
    return fixture.debugElement.query(By.directive(VenueForm)).componentInstance;
  }

  function nativeElement(): HTMLElement {
    return fixture.nativeElement as HTMLElement;
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

  function validValues(): {
    name: string;
    address: string;
    city: string;
    country: string;
    capacity: number;
  } {
    return {
      name: 'New Hall',
      address: '2 Park Lane',
      city: 'Springfield',
      country: 'USA',
      capacity: 300,
    };
  }

  it('creates a venue: POSTs /venues and navigates to /admin/venues', async () => {
    await createAt('/admin/venues/new');
    fixture.detectChanges();

    const navigationDone = waitForNavigation('/admin/venues');
    component().form.setValue(validValues());
    fixture.detectChanges();
    clickButton('Save venue');

    const req = httpMock.expectOne('http://localhost:8080/venues');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({
      name: 'New Hall',
      address: '2 Park Lane',
      city: 'Springfield',
      country: 'USA',
      capacity: 300,
    });
    req.flush(venue);

    await navigationDone;
    expect(router.url).toBe('/admin/venues');
  });

  it('rejects a capacity below 1', async () => {
    await createAt('/admin/venues/new');
    fixture.detectChanges();

    const values = validValues();
    values.capacity = 0;
    component().form.setValue(values);
    component().form.controls.capacity.markAsTouched();
    fixture.detectChanges();

    expect(component().form.controls.capacity.invalid).toBe(true);
    expect(text()).toContain('Capacity must be at least 1');
    expect(router.url).toBe('/admin/venues/new');
  });

  it('edit mode loads the venue and PUTs the update', async () => {
    await createAt('/admin/venues/ven-1');
    httpMock.expectOne('http://localhost:8080/venues/ven-1').flush(venue);
    fixture.detectChanges();

    expect(component().form.controls.name.value).toBe('Riverside Amphitheatre');
    expect(component().form.controls.capacity.value).toBe(500);

    component().form.controls.name.setValue('Renamed Hall');
    fixture.detectChanges();

    const navigationDone = waitForNavigation('/admin/venues');
    clickButton('Save venue');

    const req = httpMock.expectOne('http://localhost:8080/venues/ven-1');
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toEqual({
      name: 'Renamed Hall',
      address: '1 Park Lane',
      city: 'Springfield',
      country: 'USA',
      capacity: 500,
    });
    req.flush({ ...venue, name: 'Renamed Hall' });

    await navigationDone;
    expect(router.url).toBe('/admin/venues');
  });
});
