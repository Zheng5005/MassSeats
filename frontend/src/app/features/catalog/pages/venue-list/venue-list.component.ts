import { Component, inject, PLATFORM_ID, signal } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { OnInit } from '@angular/core';
import { RouterLink } from '@angular/router';

import { CatalogService } from '../../catalog.api';
import { Venue } from '../../../../shared/models/catalog.models';
import { errorMessage } from '../../../../shared/utils/format';

@Component({
  selector: 'app-venue-list',
  imports: [RouterLink],
  templateUrl: './venue-list.html',
})
export class VenueList implements OnInit {
  private readonly platformId = inject(PLATFORM_ID);
  private readonly catalog = inject(CatalogService);

  readonly venues = signal<Venue[]>([]);
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
}
