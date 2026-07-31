using PaymentService.Application.DTOs;
using PaymentService.Application.Interfaces;
using PaymentService.Application.Services;
using PaymentService.Domain.Entities;
using PaymentService.Domain.Exceptions;
using PaymentService.Domain.Interfaces;

namespace PaymentService.Infrastructure.Tests;

public sealed class PaymentInitiateRaceTests
{
    [Fact]
    public async Task InitiateAsync_WhenConcurrentCreateLosesRace_ReturnsExistingPayment()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var existing = CreatePayment();
        var repository = new FakePaymentRepository
        {
            Existing = existing,
            LoseRaceOnFirstSave = true
        };
        var gateway = new FakePaymentGateway();
        var service = new PaymentAppService(repository, gateway);

        var result = await service.InitiateAsync(
            new InitiatePaymentRequest(existing.BookingId, 50m, "USD"),
            cancellationToken);

        // The loser returns the committed payment (same booking), not a 500.
        Assert.Equal(existing.Id, result.Id);
        Assert.Equal(existing.BookingId, result.BookingId);
        Assert.Equal("Pending", result.Status);
        Assert.Equal(1, gateway.CallCount);
    }

    [Fact]
    public async Task InitiateAsync_WhenPaymentAlreadyExists_DoesNotCallGateway()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var existing = CreatePayment();
        var repository = new FakePaymentRepository { Existing = existing };
        var gateway = new FakePaymentGateway();
        var service = new PaymentAppService(repository, gateway);

        var result = await service.InitiateAsync(
            new InitiatePaymentRequest(existing.BookingId, 50m, "USD"),
            cancellationToken);

        Assert.Equal(existing.Id, result.Id);
        Assert.Equal(0, gateway.CallCount);
    }

    private static Payment CreatePayment()
    {
        var payment = Payment.Create(
            Guid.NewGuid(),
            $"pi_test_{Guid.NewGuid():N}",
            50m,
            "USD");
        return payment;
    }

    private sealed class FakePaymentRepository : IPaymentRepository
    {
        public Payment? Existing { get; init; }
        public bool LoseRaceOnFirstSave { get; init; }
        private int _getByBookingIdCalls;

        public Task<Payment?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<Payment?> GetByBookingIdAsync(
            Guid bookingId,
            CancellationToken ct = default)
        {
            _getByBookingIdCalls++;
            // First check sees nothing (the concurrent commit has not landed
            // yet); after the losing save, the committed payment exists.
            return Task.FromResult(
                LoseRaceOnFirstSave && _getByBookingIdCalls == 1 ? null : Existing);
        }

        public Task<Payment?> GetByStripePaymentIntentIdAsync(
            string stripePaymentIntentId,
            CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task AddAsync(Payment payment, CancellationToken ct = default) =>
            Task.CompletedTask;

        public void Update(Payment payment) { }

        public Task<int> SaveChangesAsync(CancellationToken ct = default) =>
            LoseRaceOnFirstSave
                ? throw new DuplicatePaymentException(Existing!.BookingId)
                : Task.FromResult(1);
    }

    private sealed class FakePaymentGateway : IPaymentGateway
    {
        public int CallCount { get; private set; }

        public Task<string> CreatePaymentIntentAsync(
            Guid bookingId,
            decimal amount,
            string currency,
            CancellationToken ct = default)
        {
            CallCount++;
            return Task.FromResult($"pi_test_{bookingId:N}");
        }

        public Task<StripeWebhookResult?> VerifyWebhookAsync(
            string rawBody,
            string signatureHeader,
            CancellationToken ct = default) =>
            throw new NotSupportedException();
    }
}
