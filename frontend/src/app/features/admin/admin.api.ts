import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';

import { ApiClient } from '../../core/api/api-client';
import { Event, Venue } from '../../shared/models/catalog.models';
import {
  CreateEventRequest,
  UpdateEventPricingRequest,
  UpdateEventRequest,
  VenueRequest,
} from '../../shared/models/admin.models';

/**
 * Admin write operations for events and venues. Reads (listEvents, getEvent,
 * listVenues, getVenue, listCategories) go through CatalogService — this
 * service only owns mutations.
 */
@Injectable({ providedIn: 'root' })
export class AdminService {
  private readonly api = inject(ApiClient);

  createEvent(body: CreateEventRequest): Observable<Event> {
    return this.api.post<Event>('/events', body);
  }

  updateEvent(id: string, body: UpdateEventRequest): Observable<Event> {
    return this.api.put<Event>(`/events/${id}`, body);
  }

  updateEventPricing(id: string, body: UpdateEventPricingRequest): Observable<Event> {
    return this.api.put<Event>(`/events/${id}/pricing`, body);
  }

  deleteEvent(id: string): Observable<void> {
    return this.api.delete<void>(`/events/${id}`);
  }

  createVenue(body: VenueRequest): Observable<Venue> {
    return this.api.post<Venue>('/venues', body);
  }

  updateVenue(id: string, body: VenueRequest): Observable<Venue> {
    return this.api.put<Venue>(`/venues/${id}`, body);
  }

  deleteVenue(id: string): Observable<void> {
    return this.api.delete<void>(`/venues/${id}`);
  }
}
