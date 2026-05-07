using System.ComponentModel.DataAnnotations;

using AutoFixture.Xunit2;

using PaymentGateway.Api.Models.Requests;

namespace PaymentGateway.Api.Tests.Models.Requests;

public class PostPaymentRequestTests
{
    [Theory, AutoData]
    public void Validate_ValidDetails_ReturnsNoError(int amount)
    {
        var request = new PostPaymentRequest
        {
            CardNumber = "1234567890123456",
            ExpiryMonth = 5,
            ExpiryYear = 2047,
            Currency = "GBP",
            Amount = amount,
            Cvv = 135
        };
        var error = Validate(request);
        Assert.Empty(error);
    }

    [Fact]
    public void Validate_NoCardNumber_ReturnsError()
    {
        var request = new PostPaymentRequest
        {
            CardNumber = "",
            ExpiryMonth = 12,
            ExpiryYear = 2047,
            Currency = "GBP",
            Amount = 1000,
            Cvv = 123
        };
        var error = Validate(request);
        Assert.Equal(3, error.Count);
        Assert.Equal("Card number is required", error[0].ErrorMessage);
        Assert.Equal("Card number must be between 14 and 19 characters", error[1].ErrorMessage);
        Assert.Equal("Card number must contain only numeric characters", error[2].ErrorMessage);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("123")]
    [InlineData("1234567890123")]
    [InlineData("12345678901234567890")]
    [InlineData("123456789012345678901234567890")]
    public void Validate_CardNumberWrongLength_ReturnsError(string cardNumber)
    {
        var request = new PostPaymentRequest
        {
            CardNumber = cardNumber,
            ExpiryMonth = 12,
            ExpiryYear = 2047,
            Currency = "GBP",
            Amount = 1000,
            Cvv = 123
        };
        var error = Validate(request);
        Assert.Single(error);
        Assert.Equal("Card number must be between 14 and 19 characters", error[0].ErrorMessage);
    }

    [Theory]
    [InlineData("                ")]
    [InlineData("123456789A012345")]
    [InlineData("B123456789012345")]
    [InlineData("123456789012345C")]
    [InlineData("ABCDEFGHIJKLMNOP")]
    [InlineData("1235!678-A1=34+5")]
    public void Validate_CardNumberNotNumbers_ReturnsError(string cardNumber)
    {
        var request = new PostPaymentRequest
        {
            CardNumber = cardNumber,
            ExpiryMonth = 12,
            ExpiryYear = 2047,
            Currency = "GBP",
            Amount = 1000,
            Cvv = 123
        };
        var error = Validate(request);
        Assert.Single(error);
        Assert.Equal("Card number must contain only numeric characters", error[0].ErrorMessage);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(13)]
    [InlineData(-1235)]
    public void Validate_InvalidMonth_ReturnsError(int expiryMonth)
    {
        var request = new PostPaymentRequest
        {
            CardNumber = "1234567890123456",
            ExpiryMonth = expiryMonth,
            ExpiryYear = 2047,
            Currency = "GBP",
            Amount = 1000,
            Cvv = 123
        };
        var error = Validate(request);
        Assert.Single(error);
        Assert.Equal("Expiry month must be between 1 and 12", error[0].ErrorMessage);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1235)]
    public void Validate_InvalidYear_ReturnsError(int expiryYear)
    {
        var request = new PostPaymentRequest
        {
            CardNumber = "1234567890123456",
            ExpiryMonth = 5,
            ExpiryYear = expiryYear,
            Currency = "GBP",
            Amount = 1000,
            Cvv = 123
        };
        var error = Validate(request);
        Assert.Single(error);
        Assert.Equal("Expiry year must be a positive number", error[0].ErrorMessage);
    }

    [Theory]
    [InlineData(5, 124)]
    [InlineData(5, 2020)]
    public void Validate_CardExpired_ReturnsError(int expiryMonth, int expiryYear)
    {
        var request = new PostPaymentRequest
        {
            CardNumber = "1234567890123456",
            ExpiryMonth = expiryMonth,
            ExpiryYear = expiryYear,
            Currency = "GBP",
            Amount = 1000,
            Cvv = 123
        };
        var error = Validate(request);
        Assert.Single(error);
        Assert.Equal("Card has expired", error[0].ErrorMessage);
    }

    [Theory]
    [InlineData(" ")]
    [InlineData("ABCD")]
    [InlineData("GB")]
    [InlineData("Pounds")]
    public void Validate_InvalidCurrencyLength_ReturnsError(string currency)
    {
        var request = new PostPaymentRequest
        {
            CardNumber = "1234567890123456",
            ExpiryMonth = 5,
            ExpiryYear = 2047,
            Currency = currency,
            Amount = 1000,
            Cvv = 123
        };
        var error = Validate(request);
        Assert.Single(error);
        Assert.Equal("Currency must be a 3 letter ISO currency code", error[0].ErrorMessage);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(99)]
    [InlineData(12345)]
    public void Validate_InvalidCvvLength_ReturnsError(int cvv)
    {
        var request = new PostPaymentRequest
        {
            CardNumber = "1234567890123456",
            ExpiryMonth = 5,
            ExpiryYear = 2047,
            Currency = "GBP",
            Amount = 1000,
            Cvv = cvv
        };
        var error = Validate(request);
        Assert.Single(error);
        Assert.Equal("CVV must be a valid number between 3 and 4 characters", error[0].ErrorMessage);
    }

    private static List<ValidationResult> Validate(PostPaymentRequest request)
    {
        var validationResults = new List<ValidationResult>();
        var validationContext = new ValidationContext(request);
        Validator.TryValidateObject(request, validationContext, validationResults, true);
        return validationResults;
    }
}