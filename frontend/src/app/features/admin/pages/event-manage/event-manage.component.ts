import { Component, inject, PLATFORM_ID, signal } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { OnInit } from '@angular/core';
import { RouterLink } from '@angular/router';
import { forkJoin } from 'rxjs';

import { AdminService } from '../../admin.api';
import { CatalogService } from '../../../catalog/catalog.api';
import { Event, Venue } from '../../../../shared/models/catalog.models';
import { errorMessage, formatDate, formatPrice } from '../../../../shared/utils/format';

/**
 * Admin access is auth-only for now; role-based control is planned.
 */
const AUTH_ONLY_NOTE = 'Admin access is auth-only for now; role-based control is planned.';

@Component({
  selector: 'app-event-manage',
  imports: [RouterLink],
  templateUrl: './event-manage.html',
})
export class EventManage implements OnInit {
  private readonly platformId = inject(PLATFORM_ID);
  private readonly catalog = inject(CatalogService);
  private readonly admin = inject(AdminService);

  protected readonly events = signal<Event[]>([]);
  protected readonly venues = signal<Venue[]>([]);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly deleteError = signal<string | null>(null);
  protected readonly confirmingId = signal<string | null>(null);
  protected readonly deletingId = signal<string | null>(null);
  protected readonly note = AUTH_ONLY_NOTE;

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
    this.deleteError.set(null);
    forkJoin({
      events: this.catalog.listEvents(),
      venues: this.catalog.listVenues(),
    }).subscribe({
      next: ({ events, venues }) => {
        this.events.set(events);
        this.venues.set(venues);
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

  confirmDelete(id: string): void {
    this.confirmingId.set(id);
  }

  cancelDelete(): void {
    this.confirmingId.set(null);
  }

  delete(id: string): void {
    this.deletingId.set(id);
    this.deleteError.set(null);
    this.admin.deleteEvent(id).subscribe({
      next: () => {
        this.events.set(this.events().filter((event) => event.id !== id));
        this.confirmingId.set(null);
        this.deletingId.set(null);
      },
      error: (err: unknown) => {
        this.deletingId.set(null);
        this.deleteError.set(errorMessage(err));
      },
    });
  }
}
