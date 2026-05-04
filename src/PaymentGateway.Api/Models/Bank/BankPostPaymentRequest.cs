using JetBrains.Annotations;

namespace PaymentGateway.Api.Models.Bank;

// ReSharper disable InconsistentNaming
[PublicAPI]
public class BankPostPaymentRequest
{
    public required string card_number { get; set; }
    public required string expiry_date { get; set; }
    public required string Currency { get; set; }
    public int Amount { get; set; }
    public int Cvv { get; set; }
}