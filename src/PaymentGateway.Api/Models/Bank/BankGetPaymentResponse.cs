using JetBrains.Annotations;

using PaymentGateway.Api.Enums;

namespace PaymentGateway.Api.Models.Bank;

// ReSharper disable InconsistentNaming
[PublicAPI]
public class BankGetPaymentResponse
{
    public Guid Id { get; set; }
    public PaymentStatus Status { get; set; }
    public int CardNumberLastFour { get; set; }
    public int ExpiryMonth { get; set; }
    public int ExpiryYear { get; set; }
    public string Currency { get; set; } = "";
    public int Amount { get; set; }
    public required string authorization_code { get; set; }
}