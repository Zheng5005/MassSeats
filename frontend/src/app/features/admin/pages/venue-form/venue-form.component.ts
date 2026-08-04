import { Component, inject, OnInit, PLATFORM_ID, signal } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { forkJoin, of } from 'rxjs';

import { AdminService } from '../../admin.api';
import { CatalogService } from '../../../catalog/catalog.api';
import { Venue } from '../../../../shared/models/catalog.models';
import { VenueRequest } from '../../../../shared/models/admin.models';
import { errorMessage } from '../../../../shared/utils/format';

function firstError(
  control: FormControl<string | null>,
  messages: Record<string, string>,
): string | null {
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
  selector: 'app-venue-form',
  imports: [ReactiveFormsModule],
  templateUrl: './venue-form.html',
})
export class VenueForm implements OnInit {
  private readonly platformId = inject(PLATFORM_ID);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly catalog = inject(CatalogService);
  private readonly admin = inject(AdminService);

  protected readonly venueId = this.route.snapshot.paramMap.get('id');
  protected readonly isCreate = this.venueId === null;

  readonly form = new FormGroup({
    name: new FormControl('', { validators: [Validators.required] }),
    address: new FormControl('', { validators: [Validators.required] }),
    city: new FormControl('', { validators: [Validators.required] }),
    country: new FormControl('', { validators: [Validators.required] }),
    capacity: new FormControl<number | null>(null, {
      validators: [Validators.required, Validators.min(1)],
    }),
  });

  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly saving = signal(false);
  protected readonly saveError = signal<string | null>(null);

  ngOnInit(): void {
    if (isPlatformBrowser(this.platformId)) {
      this.load();
    }
  }

  load(): void {
    this.loading.set(true);
    this.error.set(null);
    const venue$ = this.venueId ? this.catalog.getVenue(this.venueId) : of(null);
    forkJoin({ venue: venue$ }).subscribe({
      next: ({ venue }) => {
        if (venue) {
          this.form.patchValue({
            name: venue.name,
            address: venue.address,
            city: venue.city,
            country: venue.country,
            capacity: venue.capacity,
          });
        }
        this.loading.set(false);
      },
      error: (err: unknown) => {
        this.error.set(errorMessage(err));
        this.loading.set(false);
      },
    });
  }

  save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    this.saving.set(true);
    this.saveError.set(null);
    const body = this.buildPayload();
    if (this.venueId) {
      this.admin.updateVenue(this.venueId, body).subscribe({
        next: () => this.router.navigateByUrl('/admin/venues'),
        error: (err: unknown) => {
          this.saving.set(false);
          this.saveError.set(errorMessage(err));
        },
      });
    } else {
      this.admin.createVenue(body).subscribe({
        next: () => this.router.navigateByUrl('/admin/venues'),
        error: (err: unknown) => {
          this.saving.set(false);
          this.saveError.set(errorMessage(err));
        },
      });
    }
  }

  private buildPayload(): VenueRequest {
    const raw = this.form.getRawValue();
    return {
      name: (raw.name ?? '').trim(),
      address: (raw.address ?? '').trim(),
      city: (raw.city ?? '').trim(),
      country: (raw.country ?? '').trim(),
      capacity: Number(raw.capacity),
    };
  }

  protected nameError(): string | null {
    return firstError(this.form.controls.name, { required: 'Name is required' });
  }

  protected addressError(): string | null {
    return firstError(this.form.controls.address, { required: 'Address is required' });
  }

  protected cityError(): string | null {
    return firstError(this.form.controls.city, { required: 'City is required' });
  }

  protected countryError(): string | null {
    return firstError(this.form.controls.country, { required: 'Country is required' });
  }

  protected capacityError(): string | null {
    const control = this.form.controls.capacity;
    if (!control.invalid || !(control.dirty || control.touched)) {
      return null;
    }
    if (control.hasError('required')) {
      return 'Capacity is required';
    }
    if (control.hasError('min')) {
      return 'Capacity must be at least 1';
    }
    return null;
  }
}
