using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

using JetBrains.Annotations;

namespace PaymentGateway.Api.Models.Requests;

[PublicAPI]
public partial class PostPaymentRequest : IValidatableObject
{
    public required string CardNumber { get; set; }
    public int ExpiryMonth { get; set; }
    public int ExpiryYear { get; set; }
    public required string Currency { get; set; }
    public int Amount { get; set; }
    public int Cvv { get; set; }
    
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (CardNumber == string.Empty)
            yield return new ValidationResult("Card number is required", [nameof(CardNumber)]);
        
        if (CardNumber.Length is < 14 or > 19)
            yield return new ValidationResult("Card number must be between 14 and 19 characters", [nameof(CardNumber)]);

        if (!NumbersOnlyRegex().IsMatch(CardNumber))
            yield return new ValidationResult("Card number must contain only numeric characters", [nameof(CardNumber)]);

        var isDate = true;
        if (ExpiryMonth is < 1 or > 12)
        {
            isDate = false;
            yield return new ValidationResult("Expiry month must be between 1 and 12", [nameof(ExpiryMonth)]);
        }

        if (ExpiryYear < 1)
        {
            isDate = false;
            yield return new ValidationResult("Expiry year must be a positive number", [nameof(ExpiryYear)]);
        }

        if (isDate && !(new DateTime(ExpiryYear, ExpiryMonth, 1) > DateTime.Now))
            yield return new ValidationResult("Card has expired", [nameof(ExpiryMonth), nameof(ExpiryYear)]);
        
        if (Currency.Length != 3)
            yield return new ValidationResult("Currency must be a 3 letter ISO currency code", [nameof(Currency)]);

        if (Cvv is < 100 or >= 10000)
            yield return new ValidationResult("CVV must be a valid number between 3 and 4 characters", [nameof(Cvv)]);
    }

    [GeneratedRegex(@"^\d+$")]
    private static partial Regex NumbersOnlyRegex();
}