namespace PaymentService.Application.DTOs;

/// <summary>Client secret + intent id returned only to the owning user while the payment is Pending.</summary>
public sealed record PaymentClientSecretResult(string ClientSecret, string PaymentIntentId);
