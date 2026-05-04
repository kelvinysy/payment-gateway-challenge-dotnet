namespace PaymentGateway.Api.Options;

public class CurrencyCodes
{
    public const string Name = "CurrencyCodes";
    public HashSet<string> Codes { get; init; } = [];
}