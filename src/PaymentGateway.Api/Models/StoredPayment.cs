using PaymentGateway.Api.Models.Responses;

namespace PaymentGateway.Api.Models;

public class StoredPayment
{
    public required PostPaymentResponse PaymentResponse { get; init; }
    // ReSharper disable once UnusedAutoPropertyAccessor.Global
    public string AuthorizationCode { get; set; } = ""; // unused (for the test)
}