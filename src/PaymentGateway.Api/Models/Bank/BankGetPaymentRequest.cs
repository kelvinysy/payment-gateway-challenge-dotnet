using JetBrains.Annotations;

namespace PaymentGateway.Api.Models.Bank;

[PublicAPI]
public class BankGetPaymentRequest
{
    public Guid Id { get; set; }
}