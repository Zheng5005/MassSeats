import { Component, inject, PLATFORM_ID, signal } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { OnInit } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { forkJoin } from 'rxjs';

import { ApiError } from '../../../../core/api/error.model';
import { CatalogService } from '../../catalog.api';
import { Category, Event, Venue } from '../../../../shared/models/catalog.models';
import { errorMessage, formatDate, formatPrice, formatTime } from '../../../../shared/utils/format';

@Component({
  selector: 'app-event-detail',
  imports: [RouterLink],
  templateUrl: './event-detail.html',
})
export class EventDetail implements OnInit {
  private readonly platformId = inject(PLATFORM_ID);
  private readonly route = inject(ActivatedRoute);
  private readonly catalog = inject(CatalogService);

  readonly event = signal<Event | null>(null);
  readonly venues = signal<Venue[]>([]);
  readonly categories = signal<Category[]>([]);
  readonly loading = signal(true);
  readonly notFound = signal(false);
  readonly error = signal<string | null>(null);

  protected readonly formatDate = formatDate;
  protected readonly formatTime = formatTime;
  protected readonly formatPrice = formatPrice;

  ngOnInit(): void {
    if (isPlatformBrowser(this.platformId)) {
      this.load();
    }
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
    forkJoin({
      event: this.catalog.getEvent(id),
      venues: this.catalog.listVenues(),
      categories: this.catalog.listCategories(),
    }).subscribe({
      next: ({ event, venues, categories }) => {
        this.event.set(event);
        this.venues.set(venues);
        this.categories.set(categories);
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

  venueName(venueId: string): string {
    return this.venues().find((venue) => venue.id === venueId)?.name ?? 'Unknown venue';
  }

  categoryName(categoryId: string): string {
    return (
      this.categories().find((category) => category.id === categoryId)?.name ?? 'Uncategorized'
    );
  }

  availableRatio(): number {
    const total = this.event()?.totalSeats ?? 0;
    if (total <= 0) {
      return 0;
    }
    return Math.min(1, Math.max(0, (this.event()?.availableSeats ?? 0) / total));
  }

  availabilityColor(): string {
    const ratio = this.availableRatio();
    if (ratio <= 0.2) {
      return 'bg-rose-500';
    }
    if (ratio <= 0.5) {
      return 'bg-amber-500';
    }
    return 'bg-emerald-500';
  }
}
