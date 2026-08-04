import { Component, inject, OnInit, PLATFORM_ID, signal } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import {
  AbstractControl,
  FormControl,
  FormGroup,
  ReactiveFormsModule,
  ValidationErrors,
  Validators,
} from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { forkJoin, of } from 'rxjs';

import { AdminService } from '../../admin.api';
import { CatalogService } from '../../../catalog/catalog.api';
import { Category, Event, Venue } from '../../../../shared/models/catalog.models';
import { CreateEventRequest, UpdateEventRequest } from '../../../../shared/models/admin.models';
import { errorMessage, toIsoDateTime, toLocalDateTimeInput } from '../../../../shared/utils/format';

function emptyToNull(value: string | null | undefined): string | null {
  const trimmed = value?.trim() ?? '';
  return trimmed.length > 0 ? trimmed : null;
}

function futureDateValidator(control: AbstractControl): ValidationErrors | null {
  const value = control.value;
  if (typeof value !== 'string' || value.length === 0) {
    return null;
  }
  const time = new Date(value).getTime();
  if (Number.isNaN(time)) {
    return null;
  }
  return time > Date.now() ? null : { futureDate: true };
}

function firstError(control: AbstractControl, messages: Record<string, string>): string | null {
  if (!control.invalid || !(control.dirty || control.touched)) {
    return null;
  }
  for (const [key, message] of Object.entries(messages)) {
    if (control.hasError(key)) {
      return message;
    }
  }
  return null;
}

@Component({
  selector: 'app-event-form',
  imports: [ReactiveFormsModule],
  templateUrl: './event-form.html',
})
export class EventForm implements OnInit {
  private readonly platformId = inject(PLATFORM_ID);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly catalog = inject(CatalogService);
  private readonly admin = inject(AdminService);

  protected readonly eventId = this.route.snapshot.paramMap.get('id');
  protected readonly isCreate = this.eventId === null;

  readonly form = new FormGroup({
    title: new FormControl('', { validators: [Validators.required] }),
    description: new FormControl(''),
    categoryId: new FormControl('', { validators: [Validators.required] }),
    venueId: new FormControl('', { validators: [Validators.required] }),
    eventDate: new FormControl('', { validators: [Validators.required, futureDateValidator] }),
    ticketPrice: new FormControl<number | null>(null, {
      validators: [Validators.required, Validators.min(0)],
    }),
    totalSeats: new FormControl<number | null>(null, {
      validators: [Validators.required, Validators.min(1)],
    }),
    bannerImage: new FormControl(''),
  });

  /**
   * Separate price control for the pricing section (edit mode only). Kept out
   * of the main form so the two saves never conflict.
   */
  readonly pricingPrice = new FormControl<number | null>(null, {
    validators: [Validators.required, Validators.min(0)],
  });

  protected readonly venues = signal<Venue[]>([]);
  protected readonly categories = signal<Category[]>([]);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly saving = signal(false);
  protected readonly saveError = signal<string | null>(null);
  protected readonly pricingSaving = signal(false);
  protected readonly pricingError = signal<string | null>(null);
  protected readonly pricingSuccess = signal(false);

  ngOnInit(): void {
    if (isPlatformBrowser(this.platformId)) {
      this.load();
    }
  }

  load(): void {
    this.loading.set(true);
    this.error.set(null);
    const event$ = this.eventId ? this.catalog.getEvent(this.eventId) : of(null);
    forkJoin({
      event: event$,
      venues: this.catalog.listVenues(),
      categories: this.catalog.listCategories(),
    }).subscribe({
      next: ({ event, venues, categories }) => {
        this.venues.set(venues);
        this.categories.set(categories);
        if (event) {
          this.patchForm(event);
        }
        this.loading.set(false);
      },
      error: (err: unknown) => {
        this.error.set(errorMessage(err));
        this.loading.set(false);
      },
    });
  }

  private patchForm(event: Event): void {
    this.form.patchValue({
      title: event.title,
      description: event.description ?? '',
      categoryId: event.categoryId,
      venueId: event.venueId,
      eventDate: toLocalDateTimeInput(event.eventDate),
      ticketPrice: event.ticketPrice,
      totalSeats: event.totalSeats,
      bannerImage: event.bannerImage ?? '',
    });
    this.pricingPrice.setValue(event.ticketPrice);
  }

  save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    this.saving.set(true);
    this.saveError.set(null);
    if (this.eventId) {
      this.admin.updateEvent(this.eventId, this.buildUpdatePayload()).subscribe({
        next: () => this.router.navigateByUrl('/admin/events'),
        error: (err: unknown) => {
          this.saving.set(false);
          this.saveError.set(errorMessage(err));
        },
      });
    } else {
      this.admin.createEvent(this.buildCreatePayload()).subscribe({
        next: () => this.router.navigateByUrl('/admin/events'),
        error: (err: unknown) => {
          this.saving.set(false);
          this.saveError.set(errorMessage(err));
        },
      });
    }
  }

  savePricing(): void {
    if (!this.eventId || this.pricingPrice.invalid) {
      return;
    }
    this.pricingSaving.set(true);
    this.pricingError.set(null);
    this.pricingSuccess.set(false);
    this.admin
      .updateEventPricing(this.eventId, { ticketPrice: Number(this.pricingPrice.value) })
      .subscribe({
        next: () => {
          this.pricingSaving.set(false);
          this.pricingSuccess.set(true);
        },
        error: (err: unknown) => {
          this.pricingSaving.set(false);
          this.pricingError.set(errorMessage(err));
        },
      });
  }

  private buildCreatePayload(): CreateEventRequest {
    const raw = this.form.getRawValue();
    return {
      title: (raw.title ?? '').trim(),
      description: emptyToNull(raw.description),
      categoryId: raw.categoryId ?? '',
      venueId: raw.venueId ?? '',
      eventDate: toIsoDateTime(raw.eventDate ?? ''),
      ticketPrice: Number(raw.ticketPrice),
      totalSeats: Number(raw.totalSeats),
      bannerImage: emptyToNull(raw.bannerImage),
    };
  }

  private buildUpdatePayload(): UpdateEventRequest {
    const raw = this.form.getRawValue();
    return {
      title: (raw.title ?? '').trim(),
      description: emptyToNull(raw.description),
      categoryId: raw.categoryId ?? '',
      venueId: raw.venueId ?? '',
      eventDate: toIsoDateTime(raw.eventDate ?? ''),
    };
  }

  protected titleError(): string | null {
    return firstError(this.form.controls.title, { required: 'Title is required' });
  }

  protected categoryError(): string | null {
    return firstError(this.form.controls.categoryId, { required: 'Category is required' });
  }

  protected venueError(): string | null {
    return firstError(this.form.controls.venueId, { required: 'Venue is required' });
  }

  protected dateError(): string | null {
    return firstError(this.form.controls.eventDate, {
      required: 'Event date is required',
      futureDate: 'Event date must be in the future',
    });
  }

  protected priceError(): string | null {
    return firstError(this.form.controls.ticketPrice, {
      required: 'Ticket price is required',
      min: 'Ticket price cannot be negative',
    });
  }

  protected seatsError(): string | null {
    return firstError(this.form.controls.totalSeats, {
      required: 'Total seats is required',
      min: 'Total seats must be at least 1',
    });
  }
}
