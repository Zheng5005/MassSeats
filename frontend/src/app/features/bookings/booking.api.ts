import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';

import { ApiClient } from '../../core/api/api-client';
import { CreateReservationRequest, Reservation } from '../../shared/models/booking.models';

/**
 * Reservation lifecycle: create, fetch and cancel bookings. Every call goes
 * through ApiClient so all requests target API_BASE_URL and share the error
 * interceptor. The gateway derives the user id from the JWT.
 */
@Injectable({ providedIn: 'root' })
export class BookingService {
  private readonly api = inject(ApiClient);

  createReservation(request: CreateReservationRequest): Observable<Reservation> {
    return this.api.post<Reservation>('/booking/reservations', request);
  }

  /**
   * The gateway scopes the result to the current user via the X-User-Id header
   * it injects from the JWT, so the frontend passes no user id here.
   */
  listReservations(): Observable<Reservation[]> {
    return this.api.get<Reservation[]>(`/booking/reservations`);
  }

  getReservation(id: string): Observable<Reservation> {
    return this.api.get<Reservation>(`/booking/reservations/${id}`);
  }

  /**
   * The backend cancels with a 204 No Content; the updated reservation must be
   * re-fetched via getReservation after this completes.
   */
  cancelReservation(id: string): Observable<void> {
    return this.api.delete<void>(`/booking/reservations/${id}`);
  }
}
