namespace PaymentGateway.Api.Extensions;

public static class StringExtensions
{
    private const int CardNumberLastFourLength = 4;
    public static string ToCardNumberLastFour(this string str) => str[^CardNumberLastFourLength..];
}