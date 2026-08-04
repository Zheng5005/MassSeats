/**
 * Write models for admin CRUD. PascalCase fields match the backend request
 * records 1:1 (see FRONTEND_PLAN §3). Reads reuse the catalog models.
 */

export interface CreateEventRequest {
  title: string;
  description?: string | null;
  categoryId: string;
  venueId: string;
  eventDate: string;
  ticketPrice: number;
  totalSeats: number;
  bannerImage?: string | null;
}

export interface UpdateEventRequest {
  title: string;
  description?: string | null;
  categoryId: string;
  venueId: string;
  eventDate: string;
}

export interface UpdateEventPricingRequest {
  ticketPrice: number;
}

export interface VenueRequest {
  name: string;
  address: string;
  city: string;
  country: string;
  capacity: number;
}
