export interface Event {
  id: string;
  title: string;
  description?: string | null;
  categoryId: string;
  venueId: string;
  eventDate: string;
  ticketPrice: number;
  totalSeats: number;
  availableSeats: number;
  bannerImage?: string | null;
  createdAt: string;
  updatedAt: string;
}

export interface Venue {
  id: string;
  name: string;
  address: string;
  city: string;
  country: string;
  capacity: number;
  createdAt: string;
  updatedAt: string;
}

export interface Category {
  id: string;
  name: string;
  description?: string | null;
  createdAt: string;
  updatedAt: string;
}
