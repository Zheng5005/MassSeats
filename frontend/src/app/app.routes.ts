import { Routes } from '@angular/router';

import { authGuard } from './core/auth/auth.guard';
import { EventDetail } from './features/catalog/pages/event-detail/event-detail.component';
import { EventList } from './features/catalog/pages/event-list/event-list.component';
import { VenueDetail } from './features/catalog/pages/venue-detail/venue-detail.component';
import { VenueList } from './features/catalog/pages/venue-list/venue-list.component';
import { PlaceholderPage } from './features/shell/placeholder-page/placeholder-page.component';

export const routes: Routes = [
  {
    path: '',
    component: EventList,
    pathMatch: 'full',
    data: { title: 'Event list' },
    canActivate: [authGuard],
  },
  {
    path: 'events/:id',
    component: EventDetail,
    data: { title: 'Event detail' },
    canActivate: [authGuard],
  },
  {
    path: 'venues',
    component: VenueList,
    data: { title: 'Venue list' },
    canActivate: [authGuard],
  },
  {
    path: 'venues/:id',
    component: VenueDetail,
    data: { title: 'Venue detail' },
    canActivate: [authGuard],
  },
  {
    path: 'login',
    component: PlaceholderPage,
    data: { title: 'Login' },
  },
  {
    path: 'register',
    component: PlaceholderPage,
    data: { title: 'Register' },
  },
  {
    path: 'profile',
    component: PlaceholderPage,
    data: { title: 'Profile' },
    canActivate: [authGuard],
  },
  {
    path: 'admin/events',
    component: PlaceholderPage,
    data: { title: 'Event management' },
    canActivate: [authGuard],
  },
  {
    path: 'admin/events/new',
    component: PlaceholderPage,
    data: { title: 'Create event' },
    canActivate: [authGuard],
  },
  {
    path: 'admin/events/:id',
    component: PlaceholderPage,
    data: { title: 'Edit event' },
    canActivate: [authGuard],
  },
  {
    path: 'admin/venues',
    component: PlaceholderPage,
    data: { title: 'Venue management' },
    canActivate: [authGuard],
  },
  {
    path: 'admin/venues/new',
    component: PlaceholderPage,
    data: { title: 'Create venue' },
    canActivate: [authGuard],
  },
  {
    path: 'admin/venues/:id',
    component: PlaceholderPage,
    data: { title: 'Edit venue' },
    canActivate: [authGuard],
  },
  {
    path: 'events/:id/book',
    component: PlaceholderPage,
    data: { title: 'Reservation create' },
    canActivate: [authGuard],
  },
  {
    path: 'reservations/:id',
    component: PlaceholderPage,
    data: { title: 'Reservation detail' },
    canActivate: [authGuard],
  },
  {
    path: '**',
    component: PlaceholderPage,
    data: { title: 'Page not found' },
  },
];
