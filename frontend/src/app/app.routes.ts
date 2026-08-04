import { Routes } from '@angular/router';

import { authGuard } from './core/auth/auth.guard';
import { EventManage } from './features/admin/pages/event-manage/event-manage.component';
import { EventForm } from './features/admin/pages/event-form/event-form.component';
import { VenueManage } from './features/admin/pages/venue-manage/venue-manage.component';
import { VenueForm } from './features/admin/pages/venue-form/venue-form.component';
import { EventDetail } from './features/catalog/pages/event-detail/event-detail.component';
import { EventList } from './features/catalog/pages/event-list/event-list.component';
import { VenueDetail } from './features/catalog/pages/venue-detail/venue-detail.component';
import { VenueList } from './features/catalog/pages/venue-list/venue-list.component';
import { Login } from './features/account/pages/login/login.component';
import { Profile } from './features/account/pages/profile/profile.component';
import { Register } from './features/account/pages/register/register.component';
import { ReservationCreate } from './features/bookings/pages/reservation-create/reservation-create.component';
import { ReservationDetail } from './features/bookings/pages/reservation-detail/reservation-detail.component';
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
    component: Login,
    data: { title: 'Login' },
  },
  {
    path: 'register',
    component: Register,
    data: { title: 'Register' },
  },
  {
    path: 'profile',
    component: Profile,
    data: { title: 'Profile' },
    canActivate: [authGuard],
  },
  {
    path: 'admin/events',
    component: EventManage,
    data: { title: 'Event management' },
    canActivate: [authGuard],
  },
  {
    path: 'admin/events/new',
    component: EventForm,
    data: { title: 'Create event' },
    canActivate: [authGuard],
  },
  {
    path: 'admin/events/:id',
    component: EventForm,
    data: { title: 'Edit event' },
    canActivate: [authGuard],
  },
  {
    path: 'admin/venues',
    component: VenueManage,
    data: { title: 'Venue management' },
    canActivate: [authGuard],
  },
  {
    path: 'admin/venues/new',
    component: VenueForm,
    data: { title: 'Create venue' },
    canActivate: [authGuard],
  },
  {
    path: 'admin/venues/:id',
    component: VenueForm,
    data: { title: 'Edit venue' },
    canActivate: [authGuard],
  },
  {
    path: 'events/:id/book',
    component: ReservationCreate,
    data: { title: 'Reservation create' },
    canActivate: [authGuard],
  },
  {
    path: 'reservations/:id',
    component: ReservationDetail,
    data: { title: 'Reservation detail' },
    canActivate: [authGuard],
  },
  {
    path: '**',
    component: PlaceholderPage,
    data: { title: 'Page not found' },
  },
];
