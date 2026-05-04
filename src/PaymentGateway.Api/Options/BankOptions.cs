namespace PaymentGateway.Api.Options;

public class BankOptions
{
    public const string Name = "Bank";

    public required string BaseUrl { get; init; }
    public required string PaymentsEndpoint { get; init; }
}