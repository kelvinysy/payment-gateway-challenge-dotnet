using System.Diagnostics;

using AutoFixture.Xunit2;

using Microsoft.Extensions.Logging;

using Moq;

using PaymentGateway.Api.Models;
using PaymentGateway.Api.Models.Responses;
using PaymentGateway.Api.Services;

namespace PaymentGateway.Api.Tests.Services;

public class PaymentsRepositoryTests
{
    private readonly PaymentsRepository _sut;

    public PaymentsRepositoryTests()
    {
        Mock<ILogger<PaymentsRepository>> mockLogger = new();
        _sut = new PaymentsRepository(new ActivitySource("test"), mockLogger.Object);
    }

    [Theory, AutoData]
    public void Get_ReturnsNull_WhenPaymentNotInRepository(PostPaymentResponse paymentResponse)
    {
        // Arrange

        // Act
        var result = _sut.Get(paymentResponse.Id);

        // Assert
        Assert.Null(result);
    }

    [Theory, AutoData]
    public void Get_ReturnsPaymentResponse_WhenPaymentInRepository(StoredPayment storedPayment)
    {
        // Arrange
        _sut.Add(storedPayment);

        // Act
        var result = _sut.Get(storedPayment.PaymentResponse.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(storedPayment.PaymentResponse.Id, result!.PaymentResponse.Id);
        Assert.Equal(storedPayment.PaymentResponse.Status, result.PaymentResponse.Status);
        Assert.Equal(storedPayment.PaymentResponse.CardNumberLastFour, result.PaymentResponse.CardNumberLastFour);
        Assert.Equal(storedPayment.PaymentResponse.ExpiryMonth, result.PaymentResponse.ExpiryMonth);
        Assert.Equal(storedPayment.PaymentResponse.ExpiryYear, result.PaymentResponse.ExpiryYear);
        Assert.Equal(storedPayment.PaymentResponse.Currency, result.PaymentResponse.Currency);
        Assert.Equal(storedPayment.PaymentResponse.Amount, result.PaymentResponse.Amount);
    }

    [Theory, AutoData]
    public void Add_AddsPaymentToRepository(StoredPayment paymentResponse)
    {
        // Arrange

        // Act
        var nullResult = _sut.Get(paymentResponse.PaymentResponse.Id);

        // Assert
        Assert.Null(nullResult);

        // Arrange
        _sut.Add(paymentResponse);

        // Act
        var result = _sut.Get(paymentResponse.PaymentResponse.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(paymentResponse.PaymentResponse.Id, result!.PaymentResponse.Id);
        Assert.Equal(paymentResponse.PaymentResponse.Status, result.PaymentResponse.Status);
        Assert.Equal(paymentResponse.PaymentResponse.CardNumberLastFour, result.PaymentResponse.CardNumberLastFour);
        Assert.Equal(paymentResponse.PaymentResponse.ExpiryMonth, result.PaymentResponse.ExpiryMonth);
        Assert.Equal(paymentResponse.PaymentResponse.ExpiryYear, result.PaymentResponse.ExpiryYear);
        Assert.Equal(paymentResponse.PaymentResponse.Currency, result.PaymentResponse.Currency);
        Assert.Equal(paymentResponse.PaymentResponse.Amount, result.PaymentResponse.Amount);
    }
}