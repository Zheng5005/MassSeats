import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';

import { ApiClient } from '../../core/api/api-client';
import { Payment } from '../../shared/models/booking.models';

/**
 * Read-only payment access, used to show the payment state of a confirmed
 * reservation. All calls go through ApiClient so they target API_BASE_URL and
 * share the error interceptor.
 */
@Injectable({ providedIn: 'root' })
export class PaymentService {
  private readonly api = inject(ApiClient);

  getPayment(id: string): Observable<Payment> {
    return this.api.get<Payment>(`/payments/${id}`);
  }
}
