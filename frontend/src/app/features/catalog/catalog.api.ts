import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';

import { ApiClient } from '../../core/api/api-client';
import { Category, Event, Venue } from '../../shared/models/catalog.models';

/**
 * Read-only catalog access. Every call goes through ApiClient so all requests
 * target API_BASE_URL and share the error interceptor.
 */
@Injectable({ providedIn: 'root' })
export class CatalogService {
  private readonly api = inject(ApiClient);

  listEvents(): Observable<Event[]> {
    return this.api.get<Event[]>('/events');
  }

  getEvent(id: string): Observable<Event> {
    return this.api.get<Event>(`/events/${id}`);
  }

  listVenues(): Observable<Venue[]> {
    return this.api.get<Venue[]>('/venues');
  }

  getVenue(id: string): Observable<Venue> {
    return this.api.get<Venue>(`/venues/${id}`);
  }

  listCategories(): Observable<Category[]> {
    return this.api.get<Category[]>('/categories');
  }
}
