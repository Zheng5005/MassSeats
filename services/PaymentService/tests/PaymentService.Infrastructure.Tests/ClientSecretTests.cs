using PaymentService.Application.Services;
using PaymentService.Domain.Entities;
using PaymentService.Domain.Enums;
using PaymentService.Domain.Exceptions;
using PaymentService.Domain.Interfaces;
using PaymentService.Application.Interfaces;

namespace PaymentService.Infrastructure.Tests;

public sealed class ClientSecretTests
{
    [Fact]
    public async Task GetClientSecretAsync_WhenPaymentIsPending_ReturnsSecret()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var payment = CreatePayment();
        var service = new PaymentAppService(
            new FakePaymentRepository(payment),
            new FakePaymentGateway());

        var secret = await service.GetClientSecretAsync(payment.BookingId, cancellationToken);

        Assert.Equal(payment.ClientSecret, secret);
    }

    [Fact]
    public async Task GetClientSecretAsync_WhenPaymentIsSucceeded_ReturnsNull()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var payment = CreatePayment();
        payment.Succeed("card");
        var service = new PaymentAppService(
            new FakePaymentRepository(payment),
            new FakePaymentGateway());

        var secret = await service.GetClientSecretAsync(payment.BookingId, cancellationToken);

        Assert.Null(secret);
    }

    [Fact]
    public async Task GetClientSecretAsync_WhenPaymentIsFailed_ReturnsNull()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var payment = CreatePayment();
        payment.Fail("Card declined.");
        var service = new PaymentAppService(
            new FakePaymentRepository(payment),
            new FakePaymentGateway());

        var secret = await service.GetClientSecretAsync(payment.BookingId, cancellationToken);

        Assert.Null(secret);
    }

    [Fact]
    public async Task GetClientSecretAsync_WhenNoPaymentExists_ReturnsNull()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var service = new PaymentAppService(
            new FakePaymentRepository(null),
            new FakePaymentGateway());

        var secret = await service.GetClientSecretAsync(Guid.NewGuid(), cancellationToken);

        Assert.Null(secret);
    }

    [Fact]
    public async Task GetClientSecretForUserAsync_WhenOwnedAndPending_ReturnsSecretAndIntentId()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var owner = Guid.NewGuid();
        var payment = CreatePayment(owner);
        var service = new PaymentAppService(
            new FakePaymentRepository(payment),
            new FakePaymentGateway());

        var result = await service.GetClientSecretForUserAsync(payment.BookingId, owner, cancellationToken);

        Assert.NotNull(result);
        Assert.Equal(payment.ClientSecret, result.ClientSecret);
        Assert.Equal(payment.StripePaymentIntentId, result.PaymentIntentId);
    }

    [Fact]
    public async Task GetClientSecretForUserAsync_WhenNotOwned_ReturnsNull()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var payment = CreatePayment();
        var service = new PaymentAppService(
            new FakePaymentRepository(payment),
            new FakePaymentGateway());

        var result = await service.GetClientSecretForUserAsync(
            payment.BookingId,
            Guid.NewGuid(),
            cancellationToken);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetClientSecretForUserAsync_WhenOwnedButResolved_ReturnsNull()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var owner = Guid.NewGuid();
        var payment = CreatePayment(owner);
        payment.Succeed("card");
        var service = new PaymentAppService(
            new FakePaymentRepository(payment),
            new FakePaymentGateway());

        var result = await service.GetClientSecretForUserAsync(payment.BookingId, owner, cancellationToken);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetByIdForUserAsync_WhenOwned_ReturnsPayment()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var owner = Guid.NewGuid();
        var payment = CreatePayment(owner);
        var service = new PaymentAppService(
            new FakePaymentRepository(payment),
            new FakePaymentGateway());

        var response = await service.GetByIdForUserAsync(payment.Id, owner, cancellationToken);

        Assert.NotNull(response);
        Assert.Equal(payment.Id, response.Id);
    }

    [Fact]
    public async Task GetByIdForUserAsync_WhenNotOwned_ReturnsNull()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var payment = CreatePayment();
        var service = new PaymentAppService(
            new FakePaymentRepository(payment),
            new FakePaymentGateway());

        var response = await service.GetByIdForUserAsync(
            payment.Id,
            Guid.NewGuid(),
            cancellationToken);

        Assert.Null(response);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WhenClientSecretIsMissingOrWhitespace_Throws(string? clientSecret)
    {
        var bookingId = Guid.NewGuid();

        var exception = Assert.Throws<DomainValidationException>(() =>
            Payment.Create(
                Guid.NewGuid(),
                bookingId,
                $"pi_test_{Guid.NewGuid():N}",
                clientSecret!,
                50m,
                "USD"));

        Assert.Contains("Client secret", exception.Message);
    }

    [Fact]
    public void Create_WhenClientSecretHasSurroundingWhitespace_StoresTrimmedValue()
    {
        var clientSecret = "pi_test_secret_trimmed";

        var payment = Payment.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            $"pi_test_{Guid.NewGuid():N}",
            $"  {clientSecret}  ",
            50m,
            "USD");

        Assert.Equal(clientSecret, payment.ClientSecret);
    }

    private static Payment CreatePayment(Guid? owner = null) =>
        Payment.Create(
            owner ?? Guid.NewGuid(),
            Guid.NewGuid(),
            $"pi_test_{Guid.NewGuid():N}",
            $"pi_test_{Guid.NewGuid():N}_secret_test",
            50m,
            "USD");

    private sealed class FakePaymentRepository : IPaymentRepository
    {
        private readonly Payment? _existing;

        public FakePaymentRepository(Payment? existing)
        {
            _existing = existing;
        }

        public Task<Payment?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(_existing);

        public Task<Payment?> GetByBookingIdAsync(
            Guid bookingId,
            CancellationToken ct = default) =>
            Task.FromResult(_existing);

        public Task<Payment?> GetByStripePaymentIntentIdAsync(
            string stripePaymentIntentId,
            CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task AddAsync(Payment payment, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public void Update(Payment payment) { }

        public Task<int> SaveChangesAsync(CancellationToken ct = default) =>
            throw new NotSupportedException();
    }

    private sealed class FakePaymentGateway : IPaymentGateway
    {
        public Task<PaymentIntentResult> CreatePaymentIntentAsync(
            Guid bookingId,
            decimal amount,
            string currency,
            CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<StripeWebhookResult?> VerifyWebhookAsync(
            string rawBody,
            string signatureHeader,
            CancellationToken ct = default) =>
            throw new NotSupportedException();
    }
}
