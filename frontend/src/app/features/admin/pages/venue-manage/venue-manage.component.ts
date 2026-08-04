import { Component, inject, PLATFORM_ID, signal } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { OnInit } from '@angular/core';
import { RouterLink } from '@angular/router';

import { AdminService } from '../../admin.api';
import { CatalogService } from '../../../catalog/catalog.api';
import { Venue } from '../../../../shared/models/catalog.models';
import { errorMessage } from '../../../../shared/utils/format';

/**
 * Admin access is auth-only for now; role-based control is planned.
 */
const AUTH_ONLY_NOTE = 'Admin access is auth-only for now; role-based control is planned.';

@Component({
  selector: 'app-venue-manage',
  imports: [RouterLink],
  templateUrl: './venue-manage.html',
})
export class VenueManage implements OnInit {
  private readonly platformId = inject(PLATFORM_ID);
  private readonly catalog = inject(CatalogService);
  private readonly admin = inject(AdminService);

  protected readonly venues = signal<Venue[]>([]);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly deleteError = signal<string | null>(null);
  protected readonly confirmingId = signal<string | null>(null);
  protected readonly deletingId = signal<string | null>(null);
  protected readonly note = AUTH_ONLY_NOTE;

  ngOnInit(): void {
    if (isPlatformBrowser(this.platformId)) {
      this.load();
    }
  }

  load(): void {
    this.loading.set(true);
    this.error.set(null);
    this.deleteError.set(null);
    this.catalog.listVenues().subscribe({
      next: (venues) => {
        this.venues.set(venues);
        this.loading.set(false);
      },
      error: (err: unknown) => {
        this.error.set(errorMessage(err));
        this.loading.set(false);
      },
    });
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
    this.admin.deleteVenue(id).subscribe({
      next: () => {
        this.venues.set(this.venues().filter((venue) => venue.id !== id));
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
