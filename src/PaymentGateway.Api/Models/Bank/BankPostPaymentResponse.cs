using JetBrains.Annotations;

namespace PaymentGateway.Api.Models.Bank;

// ReSharper disable InconsistentNaming
[PublicAPI]
public class BankPostPaymentResponse
{
    public bool Authorized { get; init; }
    public required string authorization_code { get; init; }
}