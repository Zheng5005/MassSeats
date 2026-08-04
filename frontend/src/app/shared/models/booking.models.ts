// bookings
export interface CreateReservationRequest {
  eventId: string;
  seatSection: string;
  seatRow: string;
  seatNumber: number;
  price: number;
}
export type ReservationStatus = 'Pending' | 'Confirmed' | 'Cancelled' | 'Expired';
export interface Reservation {
  id: string;
  userId: string;
  eventId: string;
  seatSection: string;
  seatRow: string;
  seatNumber: number;
  price: number;
  status: ReservationStatus;
  paymentId?: string | null;
  reservedAt: string;
  expiresAt: string;
}

// payments
export type PaymentStatus = 'Pending' | 'Succeeded' | 'Failed' | 'Cancelled' | string;
export interface Payment {
  id: string;
  bookingId: string;
  stripePaymentIntentId: string;
  amount: number;
  currency: string;
  paymentMethod?: string | null;
  status: PaymentStatus;
  createdAt: string;
  updatedAt?: string | null;
  failureReason?: string | null;
}
