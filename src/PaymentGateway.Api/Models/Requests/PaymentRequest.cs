namespace PaymentGateway.Api.Models.Requests;

public class PaymentRequest
{
    public Guid Id { get; set; }
    public required string CardNumber { get; set; }
    public int ExpiryMonth { get; set; }
    public int ExpiryYear { get; set; }
    public required string Currency { get; set; }
    public int Amount { get; set; }
    public int Cvv { get; set; }
}