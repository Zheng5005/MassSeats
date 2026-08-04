import { Component, inject, OnDestroy, OnInit, PLATFORM_ID, signal } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';

import { ApiError } from '../../../../core/api/error.model';
import { Payment, Reservation } from '../../../../shared/models/booking.models';
import { errorMessage, formatDate, formatPrice, formatTime } from '../../../../shared/utils/format';
import { BookingService } from '../../booking.api';
import { PaymentService } from '../../payment.api';

const POLL_INTERVAL_MS = 5000;

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

  readonly reservation = signal<Reservation | null>(null);
  readonly payment = signal<Payment | null>(null);
  readonly loading = signal(true);
  readonly notFound = signal(false);
  readonly error = signal<string | null>(null);
  readonly actionError = signal<string | null>(null);
  readonly refreshError = signal<string | null>(null);
  readonly cancelling = signal(false);
  readonly refreshing = signal(false);

  protected readonly formatDate = formatDate;
  protected readonly formatTime = formatTime;
  protected readonly formatPrice = formatPrice;

  private pollTimer: ReturnType<typeof setInterval> | null = null;

  ngOnInit(): void {
    if (isPlatformBrowser(this.platformId)) {
      this.load();
    }
  }

  ngOnDestroy(): void {
    this.stopPolling();
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
    if (reservation.status === 'Confirmed' && reservation.paymentId) {
      this.loadPayment(reservation.paymentId);
    } else {
      this.payment.set(null);
    }
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
