using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

using AutoFixture;
using AutoFixture.Xunit2;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

using Moq;

using PaymentGateway.Api.Controllers;
using PaymentGateway.Api.Enums;
using PaymentGateway.Api.Extensions;
using PaymentGateway.Api.Models.Requests;
using PaymentGateway.Api.Models.Responses;
using PaymentGateway.Api.Options;
using PaymentGateway.Api.Services;

namespace PaymentGateway.Api.Tests.Controllers;

public class PaymentsControllerTests
{
    private readonly PaymentsController _sut;
    private readonly Mock<IPaymentProcessor> _mockPaymentProcessor;

    public PaymentsControllerTests()
    {
        var fixture = new Fixture();
        _mockPaymentProcessor = fixture.Freeze<Mock<IPaymentProcessor>>();
        CurrencyCodes currencyCodes = new() { Codes = ["USD", "EUR", "GBP"] };

        var currencyCodesOptions = Microsoft.Extensions.Options.Options.Create(currencyCodes);

        _sut = new PaymentsController(new ActivitySource("test"), _mockPaymentProcessor.Object, currencyCodesOptions,
            new Mock<ILogger<PaymentsController>>().Object);

        _sut.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
        _sut.ControllerContext.HttpContext.Request.Headers["UniqueKey"] = Guid.NewGuid().ToString();
    }

    [Theory, AutoData]
    public async Task PostPaymentAsync_ReturnsPaymentResponse_WhenGivenPostRequest(PostPaymentRequest request,
        PaymentStatus status, int cardNumberLastFour)
    {
        // Arrange
        request.Currency = "USD";
        var paymentRequest = new PaymentRequest
        {
            Id = Guid.NewGuid(),
            CardNumber = request.CardNumber,
            ExpiryMonth = request.ExpiryMonth,
            ExpiryYear = request.ExpiryYear,
            Currency = request.Currency,
            Amount = request.Amount,
            Cvv = request.Cvv
        };
        _mockPaymentProcessor.Setup(x => x.ProcessPayment(It.IsAny<PaymentRequest>())).ReturnsAsync(
            new PostPaymentResponse
            {
                Id = paymentRequest.Id,
                Status = status,
                CardNumberLastFour = cardNumberLastFour,
                ExpiryMonth = request.ExpiryMonth,
                ExpiryYear = request.ExpiryYear,
                Currency = request.Currency,
                Amount = request.Amount
            });

        // Act
        var response = await _sut.PostPaymentAsync(request);

        // Assert
        _mockPaymentProcessor.Verify(x => x.ProcessPayment(It.IsAny<PaymentRequest>()), Times.Once);
        Assert.Equal(typeof(OkObjectResult), response.Result!.GetType());
        var result = ((response.Result as OkObjectResult)!).Value as PostPaymentResponse;
        Assert.NotNull(result);
        Assert.Equal(status, result!.Status);
        Assert.Equal(cardNumberLastFour, result.CardNumberLastFour);
        Assert.Equal(request.ExpiryMonth, result.ExpiryMonth);
        Assert.Equal(request.ExpiryYear, result.ExpiryYear);
        Assert.Equal(request.Currency, result.Currency);
        Assert.Equal(request.Amount, result.Amount);
    }

    [Theory, AutoData]
    public async Task GetPaymentAsync_ReturnsPaymentFromProcessor_WhenGivenId(Guid id,
        PostPaymentResponse paymentResponse)
    {
        // Arrange
        paymentResponse.Id = id;
        _mockPaymentProcessor.Setup(x => x.GetPayment(id)).ReturnsAsync(paymentResponse);

        // Act
        var response = await _sut.GetPaymentAsync(id);

        // Assert
        _mockPaymentProcessor.Verify(x => x.GetPayment(id), Times.Once);
        Assert.Equal(typeof(OkObjectResult), response.Result!.GetType());
        var result = ((response.Result as OkObjectResult)!).Value as PostPaymentResponse;
        Assert.NotNull(result);
        Assert.Equal(id, result!.Id);
        Assert.Equal(paymentResponse.Status, result.Status);
        Assert.Equal(paymentResponse.CardNumberLastFour, result.CardNumberLastFour);
        Assert.Equal(paymentResponse.ExpiryMonth, result.ExpiryMonth);
        Assert.Equal(paymentResponse.ExpiryYear, result.ExpiryYear);
        Assert.Equal(paymentResponse.Currency, result.Currency);
        Assert.Equal(paymentResponse.Amount, result.Amount);
    }

    [Theory, AutoData]
    public async Task GetPaymentAsync_ReturnsNotFound_WhenProcessorReturnsNull(Guid id)
    {
        // Arrange
        _mockPaymentProcessor.Setup(x => x.GetPayment(id)).ReturnsAsync((PostPaymentResponse?)null);

        // Act
        var response = await _sut.GetPaymentAsync(id);

        // Assert
        _mockPaymentProcessor.Verify(x => x.GetPayment(id), Times.Once);
        Assert.IsType<NotFoundResult>(response.Result);
    }

    [Theory]
    [InlineAutoData("USD")]
    [InlineAutoData("EUR")]
    [InlineAutoData("GBP")]
    public async Task PostPaymentAsync_ReturnsPaymentResponse_WhenGivenValidCurrency(string currencyCode,
        PaymentRequest request, PaymentStatus status, int cardNumberLastFour)
    {
        // Arrange
        request.Currency = currencyCode;
        var postPaymentResponse = new PostPaymentResponse
        {
            Id = request.Id,
            Status = status,
            CardNumberLastFour = cardNumberLastFour,
            ExpiryMonth = request.ExpiryMonth,
            ExpiryYear = request.ExpiryYear,
            Currency = request.Currency,
            Amount = request.Amount
        };
        _mockPaymentProcessor.Setup(x => x.ProcessPayment(It.IsAny<PaymentRequest>()))
            .ReturnsAsync(postPaymentResponse);

        // Act
        var response = await _sut.PostPaymentAsync(new PostPaymentRequest
        {
            CardNumber = request.CardNumber,
            ExpiryMonth = request.ExpiryMonth,
            ExpiryYear = request.ExpiryYear,
            Currency = request.Currency,
            Amount = request.Amount,
            Cvv = request.Cvv
        });

        // Assert
        _mockPaymentProcessor.Verify(x => x.ProcessPayment(It.IsAny<PaymentRequest>()), Times.Once);
        Assert.Equal(typeof(OkObjectResult), response.Result!.GetType());
        var result = ((response.Result as OkObjectResult)!).Value as PostPaymentResponse;
        Assert.NotNull(result);
        Assert.Equal(currencyCode, result!.Currency);
    }

    [Theory]
    [InlineAutoData("ABC")]
    [InlineAutoData("CAD")]
    [InlineAutoData("JPY")]
    public async Task PostPaymentAsync_ReturnsBadRequest_WhenProcessorReturnsInvalidCurrency(string currencyCode,
        PostPaymentRequest request)
    {
        // Arrange
        request.Currency = currencyCode;

        // Act
        var response = await _sut.PostPaymentAsync(request);

        // Assert
        _mockPaymentProcessor.Verify(x => x.ProcessPayment(It.IsAny<PaymentRequest>()), Times.Never);
        Assert.IsType<BadRequestObjectResult>(response.Result);
    }

    [Theory, AutoData]
    public async Task PostPaymentAsync_ReturnsCachedResponse_WhenDuplicateRequestSentWithSameUniqueKey(
        PostPaymentRequest request,
        PaymentStatus status, int cardNumberLastFour, Guid uniqueKey)
    {
        // Arrange
        request.Currency = "USD";
        var paymentResponse = new PostPaymentResponse
        {
            Id = uniqueKey,
            Status = status,
            CardNumberLastFour = cardNumberLastFour,
            ExpiryMonth = request.ExpiryMonth,
            ExpiryYear = request.ExpiryYear,
            Currency = request.Currency,
            Amount = request.Amount
        };
        _sut.ControllerContext.HttpContext.Request.Headers["UniqueKey"] = uniqueKey.ToString();
        _mockPaymentProcessor.Setup(x => x.GetPayment(uniqueKey)).ReturnsAsync(paymentResponse);

        // Act
        var response = await _sut.PostPaymentAsync(request);

        // Assert
        _mockPaymentProcessor.Verify(x => x.ProcessPayment(It.IsAny<PaymentRequest>()), Times.Never);
        Assert.Equal(typeof(OkObjectResult), response.Result!.GetType());
        var result = ((response.Result as OkObjectResult)!).Value as PostPaymentResponse;
        Assert.NotNull(result);
        Assert.Equal(status, result!.Status);
        Assert.Equal(cardNumberLastFour, result.CardNumberLastFour);
        Assert.Equal(request.ExpiryMonth, result.ExpiryMonth);
        Assert.Equal(request.ExpiryYear, result.ExpiryYear);
        Assert.Equal(request.Currency, result.Currency);
        Assert.Equal(request.Amount, result.Amount);
    }

    [Theory, AutoData]
    public async Task PostPaymentAsync_ReturnsCachedResponse_WhenDuplicateRequestSentWithoutUniqueKey(
        PostPaymentRequest request,
        PaymentStatus status, int cardNumberLastFour, Guid uniqueKey)
    {
        // Arrange
        request.Currency = "USD";
        var paymentResponse = new PostPaymentResponse
        {
            Id = uniqueKey,
            Status = status,
            CardNumberLastFour = cardNumberLastFour,
            ExpiryMonth = request.ExpiryMonth,
            ExpiryYear = request.ExpiryYear,
            Currency = request.Currency,
            Amount = request.Amount
        };
        var transactionKey = CreateHashFromRequest(request);
        _sut.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
        _mockPaymentProcessor.Setup(x => x.GetPayment(transactionKey)).ReturnsAsync(paymentResponse);

        // Act
        var response = await _sut.PostPaymentAsync(request);

        // Assert
        _mockPaymentProcessor.Verify(x => x.ProcessPayment(It.IsAny<PaymentRequest>()), Times.Never);
        Assert.Equal(typeof(OkObjectResult), response.Result!.GetType());
        var result = ((response.Result as OkObjectResult)!).Value as PostPaymentResponse;
        Assert.NotNull(result);
        Assert.Equal(status, result!.Status);
        Assert.Equal(cardNumberLastFour, result.CardNumberLastFour);
        Assert.Equal(request.ExpiryMonth, result.ExpiryMonth);
        Assert.Equal(request.ExpiryYear, result.ExpiryYear);
        Assert.Equal(request.Currency, result.Currency);
        Assert.Equal(request.Amount, result.Amount);
    }

    [Theory, AutoData]
    public async Task PostPaymentAsync_ProcessesNewPayment_WhenDifferentCardNumberSentWithoutUniqueKey(
        PostPaymentRequest request, PaymentStatus status, Guid uniqueKey, [Range(10_000_000_000_002, 9_999_999_999_999_999_999)] ulong cardNumber)
    {
        // Arrange
        request.Currency = "USD";
        request.CardNumber = cardNumber.ToString();
        var paymentResponse = new PostPaymentResponse
        {
            Id = uniqueKey,
            Status = status,
            CardNumberLastFour = int.Parse(request.CardNumber.ToCardNumberLastFour()),
            ExpiryMonth = request.ExpiryMonth,
            ExpiryYear = request.ExpiryYear,
            Currency = request.Currency,
            Amount = request.Amount
        };
        var transactionKey = CreateHashFromRequest(request);
        _sut.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
        _mockPaymentProcessor.Setup(x => x.GetPayment(transactionKey)).ReturnsAsync(paymentResponse);
        var secondRequest = new PostPaymentRequest
        {
            CardNumber = request.CardNumber[1..] + "1",
            ExpiryMonth = request.ExpiryMonth,
            ExpiryYear = request.ExpiryYear,
            Currency = request.Currency,
            Amount = request.Amount,
            Cvv = request.Cvv
        };
        var secondResponseLastFour = int.Parse(secondRequest.CardNumber.ToCardNumberLastFour());
        _mockPaymentProcessor
            .Setup(x => x.ProcessPayment(It.Is<PaymentRequest>(paymentRequest =>
                paymentRequest.CardNumber == secondRequest.CardNumber)))
            .ReturnsAsync(
                new PostPaymentResponse
                {
                    Id = Guid.NewGuid(),
                    Status = status,
                    CardNumberLastFour = int.Parse(secondRequest.CardNumber.ToCardNumberLastFour()),
                    ExpiryMonth = request.ExpiryMonth,
                    ExpiryYear = request.ExpiryYear,
                    Currency = request.Currency,
                    Amount = request.Amount
                });

        // Act
        var response = await _sut.PostPaymentAsync(secondRequest);

        // Assert
        _mockPaymentProcessor.Verify(x => x.ProcessPayment(It.IsAny<PaymentRequest>()), Times.Once);
        Assert.Equal(typeof(OkObjectResult), response.Result!.GetType());
        var result = ((response.Result as OkObjectResult)!).Value as PostPaymentResponse;
        Assert.NotNull(result);
        Assert.Equal(secondResponseLastFour, result!.CardNumberLastFour);
    }

    [Theory, AutoData]
    public async Task PostPaymentAsync_UsesFallbackGuid_WhenNoneIsProvided(PostPaymentRequest request,
        PaymentStatus status, int cardNumberLastFour)
    {
        // Arrange
        _sut.ControllerContext.HttpContext = new DefaultHttpContext();

        request.Currency = "USD";
        var paymentRequest = new PaymentRequest
        {
            Id = Guid.NewGuid(),
            CardNumber = request.CardNumber,
            ExpiryMonth = request.ExpiryMonth,
            ExpiryYear = request.ExpiryYear,
            Currency = request.Currency,
            Amount = request.Amount,
            Cvv = request.Cvv
        };
        _mockPaymentProcessor.Setup(x => x.ProcessPayment(It.IsAny<PaymentRequest>())).ReturnsAsync(
            new PostPaymentResponse
            {
                Id = paymentRequest.Id,
                Status = status,
                CardNumberLastFour = cardNumberLastFour,
                ExpiryMonth = request.ExpiryMonth,
                ExpiryYear = request.ExpiryYear,
                Currency = request.Currency,
                Amount = request.Amount
            });

        // Act
        var response = await _sut.PostPaymentAsync(request);

        // Assert
        _mockPaymentProcessor.Verify(
            x => x.ProcessPayment(It.Is<PaymentRequest>(pr =>
                pr.Id == CreateHashFromRequest(request))), Times.Once);
        Assert.Equal(typeof(OkObjectResult), response.Result!.GetType());
        var result = ((response.Result as OkObjectResult)!).Value as PostPaymentResponse;
        Assert.NotNull(result);
        Assert.Equal(status, result!.Status);
        Assert.Equal(cardNumberLastFour, result.CardNumberLastFour);
        Assert.Equal(request.ExpiryMonth, result.ExpiryMonth);
        Assert.Equal(request.ExpiryYear, result.ExpiryYear);
        Assert.Equal(request.Currency, result.Currency);
        Assert.Equal(request.Amount, result.Amount);
    }

    private static Guid CreateHashFromRequest(PostPaymentRequest request)
    {
        byte[] hash = MD5.HashData(Encoding.UTF8.GetBytes(request.CardNumber + request.Amount + request.Currency));
        return new Guid(hash);
    }
}