import { Component, inject, PLATFORM_ID, signal } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { OnInit } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';

import { ApiError } from '../../../../core/api/error.model';
import { CatalogService } from '../../catalog.api';
import { Venue } from '../../../../shared/models/catalog.models';
import { errorMessage } from '../../../../shared/utils/format';

@Component({
  selector: 'app-venue-detail',
  imports: [RouterLink],
  templateUrl: './venue-detail.html',
})
export class VenueDetail implements OnInit {
  private readonly platformId = inject(PLATFORM_ID);
  private readonly route = inject(ActivatedRoute);
  private readonly catalog = inject(CatalogService);

  readonly venue = signal<Venue | null>(null);
  readonly loading = signal(true);
  readonly notFound = signal(false);
  readonly error = signal<string | null>(null);

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
    this.catalog.getVenue(id).subscribe({
      next: (venue) => {
        this.venue.set(venue);
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
}
