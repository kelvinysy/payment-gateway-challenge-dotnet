using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

using AutoFixture.Xunit2;

using Microsoft.Extensions.Logging;

using Moq;
using Moq.Protected;

using PaymentGateway.Api.Enums;
using PaymentGateway.Api.Extensions;
using PaymentGateway.Api.Models;
using PaymentGateway.Api.Models.Bank;
using PaymentGateway.Api.Models.Requests;
using PaymentGateway.Api.Models.Responses;
using PaymentGateway.Api.Options;
using PaymentGateway.Api.Services;

namespace PaymentGateway.Api.Tests.Services;

public class PaymentProcessorTests
{
    private readonly PaymentProcessor _sut;
    private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock;
    private readonly Mock<IPaymentsRepository> _paymentsRepository;

    public PaymentProcessorTests()
    {
        _httpMessageHandlerMock = new Mock<HttpMessageHandler>();
        HttpClient httpClient = new(_httpMessageHandlerMock.Object);
        _paymentsRepository = new Mock<IPaymentsRepository>();
        Mock<ILogger<PaymentProcessor>> mockLogger = new();

        _paymentsRepository.Setup(x => x.Add(It.IsAny<StoredPayment>()))
            .Returns((StoredPayment storedPayment) => storedPayment);

        BankOptions bankOptions = new()
        {
            BaseUrl = "http://bank.local", PaymentsEndpoint = "http://bank.local/payments"
        };

        var bankOptionsWrapper = Microsoft.Extensions.Options.Options.Create(bankOptions);

        _sut = new PaymentProcessor(
            new ActivitySource("test"),
            httpClient,
            _paymentsRepository.Object,
            bankOptionsWrapper,
            mockLogger.Object);
    }

    [Theory, AutoData]
    public async Task ProcessPayment_ReturnsPaymentResponse(string currency, int amount,
        PaymentStatus status, int cvv, string authorizationCode)
    {
        // Arrange
        var postPaymentRequest = new PaymentRequest
        {
            Id = Guid.NewGuid(),
            CardNumber = "1234123412347890",
            ExpiryMonth = 12,
            ExpiryYear = 2025,
            Currency = currency,
            Amount = amount,
            Cvv = cvv
        };
        var cardNumberLastFour = int.Parse(postPaymentRequest.CardNumber.ToCardNumberLastFour());

        var bankResponse = new BankPostPaymentResponse
        {
            Authorized = status == PaymentStatus.Authorized, authorization_code = authorizationCode
        };

        var httpResponseMessage = new HttpResponseMessage(HttpStatusCode.OK);
        httpResponseMessage.Content = new StringContent(JsonSerializer.Serialize(bankResponse));
        httpResponseMessage.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(httpResponseMessage);

        // Act
        var result = await _sut.ProcessPayment(postPaymentRequest);

        // Assert
        Assert.Equal(status == PaymentStatus.Authorized ? PaymentStatus.Authorized : PaymentStatus.Declined,
            result.Status);
        Assert.Equal(postPaymentRequest.Id, result.Id);
        Assert.Equal(cardNumberLastFour, result.CardNumberLastFour);
        Assert.Equal(postPaymentRequest.ExpiryMonth, result.ExpiryMonth);
        Assert.Equal(postPaymentRequest.ExpiryYear, result.ExpiryYear);
        Assert.Equal(postPaymentRequest.Currency, result.Currency);
        Assert.Equal(postPaymentRequest.Amount, result.Amount);
    }

    [Theory, AutoData]
    public async Task ProcessPayment_AddsPaymentResponseToPaymentsRepository(string currency,
        int amount, PaymentStatus status, int cvv, string authorizationCode)
    {
        // Arrange
        var postPaymentRequest = new PaymentRequest
        {
            Id = Guid.NewGuid(),
            CardNumber = "1234123412347890",
            ExpiryMonth = 12,
            ExpiryYear = 2025,
            Currency = currency,
            Amount = amount,
            Cvv = cvv
        };

        var bankResponse = new BankPostPaymentResponse
        {
            Authorized = status == PaymentStatus.Authorized, authorization_code = authorizationCode
        };

        var httpResponseMessage = new HttpResponseMessage(HttpStatusCode.OK);
        httpResponseMessage.Content = new StringContent(JsonSerializer.Serialize(bankResponse));
        httpResponseMessage.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(httpResponseMessage);

        // Act
        await _sut.ProcessPayment(postPaymentRequest);

        // Assert
        _paymentsRepository.Verify(x => x.Add(It.IsAny<StoredPayment>()), Times.Once);
    }

    [Theory, AutoData]
    public async Task GetPayment_ReturnsPaymentResponseWithoutHttpRequest_IfPaymentIsInRepository(
        int cardNumberLastFour, int expiryMonth, int expiryYear, string currency, int amount, PaymentStatus status,
        string authorizationCode)
    {
        // Arrange
        var paymentId = Guid.NewGuid();
        var cachedPayment = new StoredPayment
        {
            PaymentResponse = new PostPaymentResponse
            {
                Id = paymentId,
                Status = status,
                CardNumberLastFour = cardNumberLastFour,
                ExpiryMonth = expiryMonth,
                ExpiryYear = expiryYear,
                Currency = currency,
                Amount = amount
            },
            AuthorizationCode = authorizationCode
        };

        _paymentsRepository.Setup(x => x.Get(paymentId)).Returns(cachedPayment);

        // Act
        var result = await _sut.GetPayment(paymentId);

        // Assert
        Assert.Equal(paymentId, result!.Id);
        Assert.Equal(cachedPayment.PaymentResponse.Status, result.Status);
        Assert.Equal(cachedPayment.PaymentResponse.CardNumberLastFour, result.CardNumberLastFour);
        Assert.Equal(cachedPayment.PaymentResponse.ExpiryMonth, result.ExpiryMonth);
        Assert.Equal(cachedPayment.PaymentResponse.ExpiryYear, result.ExpiryYear);
        Assert.Equal(cachedPayment.PaymentResponse.Currency, result.Currency);
        Assert.Equal(cachedPayment.PaymentResponse.Amount, result.Amount);
        _httpMessageHandlerMock.Protected().Verify(
            "SendAsync",
            Times.Never(),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());
    }

    [Theory, AutoData]
    public async Task GetPayment_ReturnsPaymentResponseWithHttpRequest_IfPaymentIsNotInRepositoryAndInBank(
        int cardNumberLastFour, string currency, int amount, PaymentStatus status, int expiryMonth, int expiryYear,
        string authorizationCode)
    {
        // Arrange
        var paymentId = Guid.NewGuid();

        _paymentsRepository.Setup(x => x.Get(paymentId)).Returns((StoredPayment)null!);

        var bankResponse = new BankGetPaymentResponse
        {
            Id = paymentId,
            Status = status,
            CardNumberLastFour = cardNumberLastFour,
            ExpiryMonth = expiryMonth,
            ExpiryYear = expiryYear,
            Currency = currency,
            Amount = amount,
            authorization_code = authorizationCode
        };

        var httpResponseMessage = new HttpResponseMessage(HttpStatusCode.OK);
        httpResponseMessage.Content = new StringContent(JsonSerializer.Serialize(bankResponse));
        httpResponseMessage.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(httpResponseMessage);

        // Act
        var result = await _sut.GetPayment(paymentId);

        // Assert
        Assert.Equal(bankResponse.Status, result!.Status);
        Assert.Equal(bankResponse.CardNumberLastFour, result.CardNumberLastFour);
        Assert.Equal(bankResponse.ExpiryMonth, result.ExpiryMonth);
        Assert.Equal(bankResponse.ExpiryYear, result.ExpiryYear);
        Assert.Equal(bankResponse.Currency, result.Currency);
        Assert.Equal(bankResponse.Amount, result.Amount);
        _httpMessageHandlerMock.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());
    }

    [Theory, AutoData]
    public async Task GetPayment_AddsPaymentResponseToRepository_IfPaymentIsNotInRepositoryAndInBank(
        int cardNumberLastFour, string currency, int amount, PaymentStatus status, int expiryMonth, int expiryYear,
        string authorizationCode)
    {
        // Arrange
        var paymentId = Guid.NewGuid();

        _paymentsRepository.Setup(x => x.Get(paymentId)).Returns((StoredPayment)null!);

        var bankResponse = new BankGetPaymentResponse
        {
            Id = paymentId,
            Status = status,
            CardNumberLastFour = cardNumberLastFour,
            ExpiryMonth = expiryMonth,
            ExpiryYear = expiryYear,
            Currency = currency,
            Amount = amount,
            authorization_code = authorizationCode
        };

        var httpResponseMessage = new HttpResponseMessage(HttpStatusCode.OK);
        httpResponseMessage.Content = new StringContent(JsonSerializer.Serialize(bankResponse));
        httpResponseMessage.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(httpResponseMessage);

        // Act
        await _sut.GetPayment(paymentId);

        // Assert
        _paymentsRepository.Verify(x => x.Add(It.IsAny<StoredPayment>()), Times.Once);
    }

    [Fact]
    public async Task GetPayment_ReturnsNull_IfPaymentIsNotInRepositoryOrBank()
    {
        // Arrange
        var paymentId = Guid.NewGuid();

        _paymentsRepository.Setup(x => x.Get(paymentId)).Returns((StoredPayment)null!);

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Not found"));

        // Act
        var result = await _sut.GetPayment(paymentId);

        // Assert
        Assert.Null(result);
    }

    [Theory, AutoData]
    public async Task ProcessPayment_ReturnsDeclinedPaymentResponse_IfBankPostRequestFails(string currency, int amount,
        int cvv)
    {
        // Arrange
        var postPaymentRequest = new PaymentRequest
        {
            Id = Guid.NewGuid(),
            CardNumber = "1234123412347890",
            ExpiryMonth = 12,
            ExpiryYear = 2025,
            Currency = currency,
            Amount = amount,
            Cvv = cvv
        };

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Bank service unavailable"));

        // Act
        var result = await _sut.ProcessPayment(postPaymentRequest);

        // Assert
        Assert.Equal(PaymentStatus.Declined, result.Status);
    }
}