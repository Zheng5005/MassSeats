import { TestBed } from '@angular/core/testing';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';

import { errorInterceptor } from '../../core/api/error.interceptor';
import { Payment } from '../../shared/models/booking.models';
import { PaymentService } from './payment.api';

const payment: Payment = {
  id: 'pay-1',
  bookingId: 'res-1',
  stripePaymentIntentId: 'pi_123',
  amount: 45,
  currency: 'USD',
  paymentMethod: 'card',
  status: 'Succeeded',
  createdAt: '2026-09-12T10:01:00Z',
  updatedAt: '2026-09-12T10:02:00Z',
};

describe('PaymentService', () => {
  let service: PaymentService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([errorInterceptor])),
        provideHttpClientTesting(),
      ],
    });
    service = TestBed.inject(PaymentService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('gets a payment by id with GET /payments/:id', () => {
    let result: Payment | undefined;
    service.getPayment('pay-1').subscribe((pay) => (result = pay));

    const req = httpMock.expectOne('http://localhost:8080/payments/pay-1');
    expect(req.request.method).toBe('GET');
    req.flush(payment);

    expect(result).toEqual(payment);
  });
});
