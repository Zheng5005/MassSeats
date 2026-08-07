import {
  afterEveryRender,
  Component,
  ElementRef,
  inject,
  OnDestroy,
  OnInit,
  PLATFORM_ID,
  signal,
  viewChild,
} from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';

import {
  loadStripe,
  type Stripe,
  type StripeElements,
  type StripePaymentElement,
} from '@stripe/stripe-js';

import { ApiError } from '../../../../core/api/error.model';
import { STRIPE_PUBLISHABLE_KEY } from '../../../../core/api/api.config';
import { Payment, Reservation } from '../../../../shared/models/booking.models';
import { errorMessage, formatDate, formatPrice, formatTime } from '../../../../shared/utils/format';
import { BookingService } from '../../booking.api';
import { PaymentService } from '../../payment.api';

const POLL_INTERVAL_MS = 5000;

type CheckoutStatus = 'idle' | 'loading' | 'ready' | 'unavailable' | 'error';

@Component({
  selector: 'app-reservation-detail',
  imports: [RouterLink],
  templateUrl: './reservation-detail.html',
})
export class ReservationDetail implements OnInit, OnDestroy {
  private readonly platformId = inject(PLATFORM_ID);
  private readonly route = inject(ActivatedRoute);
  private readonly bookings = inject(BookingService);
  private readonly payments = inject(PaymentService);
  private readonly stripePublishableKey = inject(STRIPE_PUBLISHABLE_KEY);

  readonly reservation = signal<Reservation | null>(null);
  readonly payment = signal<Payment | null>(null);
  readonly loading = signal(true);
  readonly notFound = signal(false);
  readonly error = signal<string | null>(null);
  readonly actionError = signal<string | null>(null);
  readonly refreshError = signal<string | null>(null);
  readonly cancelling = signal(false);
  readonly refreshing = signal(false);

  readonly checkoutStatus = signal<CheckoutStatus>('idle');
  readonly processing = signal(false);
  readonly checkoutError = signal<string | null>(null);

  protected readonly formatDate = formatDate;
  protected readonly formatTime = formatTime;
  protected readonly formatPrice = formatPrice;

  private readonly paymentElementRef = viewChild<ElementRef<HTMLDivElement>>('paymentElement');
  private stripeInstance: Stripe | null = null;
  private elementsInstance: StripeElements | null = null;
  private paymentElement: StripePaymentElement | null = null;

  private pollTimer: ReturnType<typeof setInterval> | null = null;

  /**
   * Mounts the Stripe Payment Element once the 'ready' state has rendered the
   * #paymentElement host. Runs only in the browser; afterEveryRender is a no-op
   * during SSR/prerender.
   */
  private readonly mountCheckout = afterEveryRender(() => {
    if (this.checkoutStatus() !== 'ready' || this.paymentElement) {
      return;
    }
    const host = this.paymentElementRef()?.nativeElement;
    if (!host || !this.elementsInstance) {
      return;
    }
    this.paymentElement = this.elementsInstance.create('payment');
    this.paymentElement.mount(host);
  });

  ngOnInit(): void {
    if (isPlatformBrowser(this.platformId)) {
      this.load();
    }
  }

  ngOnDestroy(): void {
    this.stopPolling();
    this.teardownCheckout();
  }

  load(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (!id) {
      this.notFound.set(true);
      this.loading.set(false);
      return;
    }

    this.loading.set(true);
    this.notFound.set(false);
    this.error.set(null);
    this.refreshError.set(null);
    this.bookings.getReservation(id).subscribe({
      next: (reservation) => this.apply(reservation),
      error: (err: unknown) => {
        if (err instanceof ApiError && err.status === 404) {
          this.notFound.set(true);
        } else {
          this.error.set(errorMessage(err));
        }
        this.loading.set(false);
      },
    });
  }

  refresh(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (!id) {
      return;
    }
    this.refreshing.set(true);
    this.bookings.getReservation(id).subscribe({
      next: (reservation) => {
        this.refreshing.set(false);
        this.refreshError.set(null);
        this.apply(reservation);
      },
      error: (err: unknown) => {
        this.refreshing.set(false);
        this.refreshError.set(errorMessage(err));
      },
    });
  }

  cancel(): void {
    const reservation = this.reservation();
    if (!reservation || reservation.status !== 'Pending') {
      return;
    }
    this.cancelling.set(true);
    this.actionError.set(null);
    this.bookings.cancelReservation(reservation.id).subscribe({
      next: () => {
        this.cancelling.set(false);
        this.load();
      },
      error: (err: unknown) => {
        this.cancelling.set(false);
        this.actionError.set(errorMessage(err));
      },
    });
  }

  statusBadgeClass(): string {
    switch (this.reservation()?.status) {
      case 'Pending':
        return 'bg-amber-50 text-amber-700 ring-amber-200';
      case 'Confirmed':
        return 'bg-emerald-50 text-emerald-700 ring-emerald-200';
      case 'Cancelled':
        return 'bg-rose-50 text-rose-700 ring-rose-200';
      case 'Expired':
        return 'bg-slate-100 text-slate-600 ring-slate-200';
      default:
        return 'bg-slate-100 text-slate-600 ring-slate-200';
    }
  }

  paymentStatusBadgeClass(): string {
    switch (this.payment()?.status) {
      case 'Succeeded':
        return 'bg-emerald-50 text-emerald-700 ring-emerald-200';
      case 'Failed':
        return 'bg-rose-50 text-rose-700 ring-rose-200';
      case 'Cancelled':
        return 'bg-slate-100 text-slate-600 ring-slate-200';
      default:
        return 'bg-amber-50 text-amber-700 ring-amber-200';
    }
  }

  private apply(reservation: Reservation): void {
    this.reservation.set(reservation);
    this.loading.set(false);
    this.syncPolling(reservation);
    this.syncCheckout(reservation);
    if (reservation.status === 'Confirmed' && reservation.paymentId) {
      this.loadPayment(reservation.paymentId);
    } else {
      this.payment.set(null);
    }
  }

  private syncCheckout(reservation: Reservation): void {
    if (reservation.status !== 'Pending') {
      this.teardownCheckout();
      return;
    }
    if (this.checkoutStatus() === 'idle') {
      void this.setupCheckout();
    }
  }

  /**
   * Bootstraps in-browser checkout for a Pending reservation: loads Stripe and
   * the client secret in parallel, then flips to 'ready' so the Payment Element
   * mounts. Any failure (empty key, missing Stripe instance, resolved payment)
   * leaves the explanatory fallback copy visible.
   */
  private async setupCheckout(): Promise<void> {
    const id = this.route.snapshot.paramMap.get('id');
    if (!id || !isPlatformBrowser(this.platformId)) {
      return;
    }
    if (!this.stripePublishableKey) {
      this.checkoutStatus.set('unavailable');
      return;
    }
    this.checkoutStatus.set('loading');
    try {
      const [stripe, { clientSecret }] = await Promise.all([
        loadStripe(this.stripePublishableKey),
        firstValueFrom(this.payments.getClientSecret(id)),
      ]);
      if (this.checkoutStatus() !== 'loading') {
        return;
      }
      if (!stripe) {
        this.checkoutStatus.set('unavailable');
        return;
      }
      this.stripeInstance = stripe;
      this.elementsInstance = stripe.elements({ clientSecret });
      this.checkoutStatus.set('ready');
    } catch (err: unknown) {
      if (this.checkoutStatus() !== 'loading') {
        return;
      }
      if (err instanceof ApiError && err.status === 404) {
        this.checkoutStatus.set('unavailable');
      } else {
        this.checkoutStatus.set('error');
      }
    }
  }

  async pay(): Promise<void> {
    const stripe = this.stripeInstance;
    const elements = this.elementsInstance;
    const reservationId = this.reservation()?.id;
    if (!stripe || !elements || !reservationId || this.processing()) {
      return;
    }
    this.processing.set(true);
    this.checkoutError.set(null);
    try {
      const submitResult = await elements.submit();
      if (submitResult.error) {
        this.checkoutError.set(submitResult.error.message ?? 'Please review your payment details.');
        return;
      }
      const result = await stripe.confirmPayment({
        elements,
        confirmParams: {
          return_url: `${location.origin}/reservations/${reservationId}`,
        },
        redirect: 'if_required',
      });
      if (
        result.error &&
        (result.error.type === 'card_error' || result.error.type === 'validation_error')
      ) {
        this.checkoutError.set(result.error.message ?? 'Payment failed. Please try again.');
      }
    } catch {
      this.checkoutError.set('Something went wrong while processing the payment. Please try again.');
    } finally {
      this.processing.set(false);
    }
  }

  private teardownCheckout(): void {
    if (this.paymentElement) {
      this.paymentElement.destroy();
    }
    this.paymentElement = null;
    this.elementsInstance = null;
    this.stripeInstance = null;
    this.checkoutStatus.set('idle');
    this.checkoutError.set(null);
    this.processing.set(false);
  }

  private loadPayment(paymentId: string): void {
    this.payments.getPayment(paymentId).subscribe({
      next: (payment) => this.payment.set(payment),
      error: () => this.payment.set(null),
    });
  }

  private syncPolling(reservation: Reservation): void {
    if (reservation.status === 'Pending') {
      this.startPolling();
    } else {
      this.stopPolling();
    }
  }

  private startPolling(): void {
    if (this.pollTimer !== null) {
      return;
    }
    this.pollTimer = setInterval(() => this.refresh(), POLL_INTERVAL_MS);
  }

  private stopPolling(): void {
    if (this.pollTimer !== null) {
      clearInterval(this.pollTimer);
      this.pollTimer = null;
    }
  }
}
