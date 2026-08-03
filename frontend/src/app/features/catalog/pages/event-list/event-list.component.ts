import { Component, inject, PLATFORM_ID, signal } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { OnInit } from '@angular/core';
import { RouterLink } from '@angular/router';
import { forkJoin } from 'rxjs';

import { CatalogService } from '../../catalog.api';
import { Category, Event, Venue } from '../../../../shared/models/catalog.models';
import { errorMessage, formatDate, formatPrice } from '../../../../shared/utils/format';

const CHIP_STYLES: readonly string[] = [
  'bg-blue-50 text-blue-700 ring-blue-200',
  'bg-amber-50 text-amber-700 ring-amber-200',
  'bg-emerald-50 text-emerald-700 ring-emerald-200',
  'bg-violet-50 text-violet-700 ring-violet-200',
  'bg-rose-50 text-rose-700 ring-rose-200',
  'bg-cyan-50 text-cyan-700 ring-cyan-200',
];

const BANNER_GRADIENTS: readonly string[] = [
  'from-slate-900 to-blue-950',
  'from-slate-900 to-amber-950',
  'from-slate-900 to-emerald-950',
  'from-slate-900 to-violet-950',
  'from-slate-900 to-rose-950',
  'from-slate-900 to-cyan-950',
];

function hashIndex(input: string, length: number): number {
  let hash = 0;
  for (let i = 0; i < input.length; i++) {
    hash = (hash * 31 + input.charCodeAt(i)) >>> 0;
  }
  return hash % length;
}

@Component({
  selector: 'app-event-list',
  imports: [RouterLink],
  templateUrl: './event-list.html',
})
export class EventList implements OnInit {
  private readonly platformId = inject(PLATFORM_ID);
  private readonly catalog = inject(CatalogService);

  readonly events = signal<Event[]>([]);
  readonly venues = signal<Venue[]>([]);
  readonly categories = signal<Category[]>([]);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);

  protected readonly formatDate = formatDate;
  protected readonly formatPrice = formatPrice;

  ngOnInit(): void {
    if (isPlatformBrowser(this.platformId)) {
      this.load();
    }
  }

  load(): void {
    this.loading.set(true);
    this.error.set(null);
    forkJoin({
      events: this.catalog.listEvents(),
      venues: this.catalog.listVenues(),
      categories: this.catalog.listCategories(),
    }).subscribe({
      next: ({ events, venues, categories }) => {
        this.events.set(events);
        this.venues.set(venues);
        this.categories.set(categories);
        this.loading.set(false);
      },
      error: (err: unknown) => {
        this.error.set(errorMessage(err));
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

  chipStyles(categoryId: string): string {
    return CHIP_STYLES[hashIndex(categoryId, CHIP_STYLES.length)] ?? CHIP_STYLES[0];
  }

  bannerGradient(categoryId: string): string {
    return BANNER_GRADIENTS[hashIndex(categoryId, BANNER_GRADIENTS.length)] ?? BANNER_GRADIENTS[0];
  }
}
