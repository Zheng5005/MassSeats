import { Component, inject, OnInit, PLATFORM_ID, signal } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { RouterLink } from '@angular/router';

import { Reservation } from '../../../../shared/models/booking.models';
import { errorMessage, formatDate, formatPrice, formatTime } from '../../../../shared/utils/format';
import { BookingService } from '../../booking.api';

@Component({
  selector: 'app-my-reservations',
  imports: [RouterLink],
  templateUrl: './my-reservations.html',
})
export class MyReservations implements OnInit {
  private readonly platformId = inject(PLATFORM_ID);
  private readonly bookings = inject(BookingService);

  protected readonly formatDate = formatDate;
  protected readonly formatTime = formatTime;
  protected readonly formatPrice = formatPrice;

  readonly reservations = signal<Reservation[]>([]);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);

  ngOnInit(): void {
    if (isPlatformBrowser(this.platformId)) {
      this.load();
    }
  }

  load(): void {
    this.loading.set(true);
    this.error.set(null);
    this.bookings.listReservations().subscribe({
      next: (reservations) => {
        this.reservations.set(reservations);
        this.loading.set(false);
      },
      error: (err: unknown) => {
        this.error.set(errorMessage(err));
        this.loading.set(false);
      },
    });
  }

  statusBadgeClass(status: string): string {
    switch (status) {
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
}
