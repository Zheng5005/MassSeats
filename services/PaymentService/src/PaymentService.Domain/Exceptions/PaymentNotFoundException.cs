using BuildingBlocks.Domain;

namespace PaymentService.Domain.Exceptions;

public sealed class PaymentNotFoundException : DomainException
{
    public PaymentNotFoundException(Guid id)
        : base($"Payment with id '{id}' was not found.") { }
}
