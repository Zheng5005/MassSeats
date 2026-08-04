import { Component, inject, OnInit, PLATFORM_ID, signal } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';

import { ApiError } from '../../../../core/api/error.model';
import { CatalogService } from '../../../catalog/catalog.api';
import { Event } from '../../../../shared/models/catalog.models';
import { CreateReservationRequest } from '../../../../shared/models/booking.models';
import { errorMessage, formatDate, formatPrice } from '../../../../shared/utils/format';
import { BookingService } from '../../booking.api';

@Component({
  selector: 'app-reservation-create',
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './reservation-create.html',
})
export class ReservationCreate implements OnInit {
  private readonly platformId = inject(PLATFORM_ID);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly catalog = inject(CatalogService);
  private readonly bookings = inject(BookingService);

  protected readonly formatDate = formatDate;
  protected readonly formatPrice = formatPrice;

  readonly event = signal<Event | null>(null);
  readonly loading = signal(true);
  readonly notFound = signal(false);
  readonly error = signal<string | null>(null);
  readonly saving = signal(false);
  readonly saveError = signal<string | null>(null);

  /**
   * Price is disabled and prefilled from the event's ticket price; it is still
   * included in the payload via getRawValue(). The backend expects the price in
   * the request body.
   */
  readonly form = new FormGroup({
    seatSection: new FormControl('', { validators: [Validators.required] }),
    seatRow: new FormControl('', { validators: [Validators.required] }),
    seatNumber: new FormControl<number | null>(null, {
      validators: [Validators.required, Validators.min(1)],
    }),
    price: new FormControl<number | null>(
      { value: null, disabled: true },
      {
        validators: [Validators.required],
      },
    ),
  });

  ngOnInit(): void {
    if (isPlatformBrowser(this.platformId)) {
      this.load();
    }
  }

  load(): void {
    const eventId = this.route.snapshot.paramMap.get('id');
    if (!eventId) {
      this.notFound.set(true);
      this.loading.set(false);
      return;
    }

    this.loading.set(true);
    this.notFound.set(false);
    this.error.set(null);
    this.catalog.getEvent(eventId).subscribe({
      next: (event) => {
        this.event.set(event);
        this.form.controls.price.setValue(event.ticketPrice);
        this.loading.set(false);
      },
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

  bookSeat(): void {
    const eventId = this.route.snapshot.paramMap.get('id');
    if (!eventId || this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.saving.set(true);
    this.saveError.set(null);
    this.bookings.createReservation(this.buildPayload(eventId)).subscribe({
      next: (reservation) => {
        this.router.navigateByUrl(`/reservations/${reservation.id}`);
      },
      error: (err: unknown) => {
        this.saving.set(false);
        this.saveError.set(errorMessage(err));
      },
    });
  }

  private buildPayload(eventId: string): CreateReservationRequest {
    const raw = this.form.getRawValue();
    return {
      eventId,
      seatSection: (raw.seatSection ?? '').trim(),
      seatRow: (raw.seatRow ?? '').trim(),
      seatNumber: Number(raw.seatNumber),
      price: Number(raw.price),
    };
  }

  protected seatSectionError(): string | null {
    return this.fieldError('seatSection', 'Seat section is required');
  }

  protected seatRowError(): string | null {
    return this.fieldError('seatRow', 'Seat row is required');
  }

  protected seatNumberError(): string | null {
    const control = this.form.controls.seatNumber;
    if (!control.invalid || !(control.dirty || control.touched)) {
      return null;
    }
    if (control.hasError('required')) {
      return 'Seat number is required';
    }
    if (control.hasError('min')) {
      return 'Seat number must be at least 1';
    }
    return null;
  }

  private fieldError(name: 'seatSection' | 'seatRow', message: string): string | null {
    const control = this.form.controls[name];
    if (!control.invalid || !(control.dirty || control.touched)) {
      return null;
    }
    return control.hasError('required') ? message : null;
  }
}
