import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';

import { ApiClient } from '../../core/api/api-client';
import { Payment } from '../../shared/models/booking.models';

/**
 * Read-only payment access: payment state for a confirmed reservation plus the
 * Stripe client secret used to run in-browser checkout on a pending one. All
 * calls go through ApiClient so they target API_BASE_URL and share the error
 * interceptor.
 */
@Injectable({ providedIn: 'root' })
export class PaymentService {
  private readonly api = inject(ApiClient);

  getPayment(id: string): Observable<Payment> {
    return this.api.get<Payment>(`/payments/${id}`);
  }

  /**
   * Returns the Stripe client secret for a Pending payment. The backend answers
   * with 404 once the payment has been resolved, which the checkout treats as
   * "checkout unavailable".
   */
  getClientSecret(
    bookingId: string,
  ): Observable<{ clientSecret: string; paymentIntentId: string }> {
    return this.api.get<{ clientSecret: string; paymentIntentId: string }>(
      `/payments/${bookingId}/client-secret`,
    );
  }
}
