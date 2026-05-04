using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

using AutoFixture;
using AutoFixture.Xunit2;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

using PaymentGateway.Api.Controllers;
using PaymentGateway.Api.Enums;
using PaymentGateway.Api.Models;
using PaymentGateway.Api.Models.Requests;
using PaymentGateway.Api.Models.Responses;
using PaymentGateway.Api.Services;

using Xunit;

namespace PaymentGateway.Api.IntegrationTests;

public class PaymentsControllerTests
{
    private readonly HttpClient _sut;
    private readonly Guid _storedGuid = Guid.NewGuid();
    private readonly StoredPayment _storedPayment;

    public PaymentsControllerTests()
    {
        Fixture fixture = new();

        _storedPayment = new StoredPayment
        {
            PaymentResponse = new PostPaymentResponse
            {
                Id = _storedGuid,
                Status = PaymentStatus.Authorized,
                CardNumberLastFour = CreateInt(fixture, 1000, 9999),
                ExpiryMonth = CreateInt(fixture, 1, 12),
                ExpiryYear = CreateInt(fixture, 2047, 2090),
                Currency = "GBP",
                Amount = CreateInt(fixture, 1, 10000)
            },
            AuthorizationCode = Guid.NewGuid().ToString()
        };

        var logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<PaymentsRepository>();
        var paymentsRepository = new PaymentsRepository(new ActivitySource("test"), logger);
        paymentsRepository.Add(_storedPayment);

        var webApplicationFactory = new WebApplicationFactory<PaymentsController>();
        _sut = webApplicationFactory.WithWebHostBuilder(builder =>
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll(typeof(IPaymentsRepository));
                    services.AddSingleton<IPaymentsRepository>(paymentsRepository);
                }))
            .CreateClient();
    }

    [Theory, AutoData]
    public async Task ValidPostRequest_ReturnsValidResponse(
        [Range(10_000_000_000_002, 9_999_999_999_999_999_999)] ulong cardNumber, [Range(1, 12)] int expiryMonth,
        [Range(2030, 2990)] int expiryYear, [Range(1, 10000)] int amount, [Range(100, 999)] int cvv)
    {
        // Arrange
        cardNumber = cardNumber%2 == 0 ? cardNumber - 1 : cardNumber;
        var paymentRequest = new PostPaymentRequest
        {
            CardNumber = cardNumber.ToString(),
            ExpiryMonth = expiryMonth,
            ExpiryYear = expiryYear,
            Currency = "GBP",
            Amount = amount,
            Cvv = cvv
        };
        using StringContent jsonContent = new(
            JsonSerializer.Serialize(paymentRequest),
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await _sut.PostAsync("/api/Payments", jsonContent);
        var paymentResponse = await response.Content.ReadFromJsonAsync<PostPaymentResponse>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(paymentResponse);
        Assert.Equal(PaymentStatus.Authorized, paymentResponse!.Status);
        Assert.Equal(int.Parse(cardNumber.ToString()[^4..]), paymentResponse.CardNumberLastFour);
        Assert.Equal(expiryMonth, paymentResponse.ExpiryMonth);
        Assert.Equal(expiryYear, paymentResponse.ExpiryYear);
        Assert.Equal(paymentRequest.Currency, paymentResponse.Currency);
        Assert.Equal(amount, paymentResponse.Amount);
    }

    [Theory, AutoData]
    public async Task DeclinedPostRequest_ReturnsDeclinedResponse(
        [Range(10_000_000_000_002, 9_999_999_999_999_999_999)] ulong cardNumber, [Range(1, 12)] int expiryMonth,
        [Range(2030, 9999)] int expiryYear, [Range(1, 10000)] int amount, [Range(100, 999)] int cvv)
    {
        // Arrange
        cardNumber = cardNumber%2 == 1 ? cardNumber - 1 : cardNumber;
        cardNumber = cardNumber%10 == 0 ? cardNumber - 2 : cardNumber;
        var paymentRequest = new PostPaymentRequest
        {
            CardNumber = cardNumber.ToString(),
            ExpiryMonth = expiryMonth,
            ExpiryYear = expiryYear,
            Currency = "GBP",
            Amount = amount,
            Cvv = cvv
        };
        using StringContent jsonContent = new(
            JsonSerializer.Serialize(paymentRequest),
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await _sut.PostAsync("/api/Payments", jsonContent);
        var paymentResponse = await response.Content.ReadFromJsonAsync<PostPaymentResponse>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(paymentResponse);
        Assert.Equal(PaymentStatus.Declined, paymentResponse!.Status);
        Assert.Equal(int.Parse(cardNumber.ToString()[^4..]), paymentResponse.CardNumberLastFour);
        Assert.Equal(expiryMonth, paymentResponse.ExpiryMonth);
        Assert.Equal(expiryYear, paymentResponse.ExpiryYear);
        Assert.Equal(paymentRequest.Currency, paymentResponse.Currency);
        Assert.Equal(amount, paymentResponse.Amount);
    }

    [Theory, AutoData]
    public async Task RejectedPostRequest_ReturnsDeclinedResponse(
        [Range(10_000_000_000_000, 9_999_999_999_999_999_999)] ulong cardNumber, [Range(1, 12)] int expiryMonth,
        [Range(2030, 9999)] int expiryYear, [Range(1, 10000)] int amount, [Range(100, 999)] int cvv)
    {
        // Arrange
        cardNumber /= 10;
        cardNumber *= 10;
        var paymentRequest = new PostPaymentRequest
        {
            CardNumber = cardNumber.ToString(),
            ExpiryMonth = expiryMonth,
            ExpiryYear = expiryYear,
            Currency = "GBP",
            Amount = amount,
            Cvv = cvv
        };
        using StringContent jsonContent = new(
            JsonSerializer.Serialize(paymentRequest),
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await _sut.PostAsync("/api/Payments", jsonContent);
        var paymentResponse = await response.Content.ReadFromJsonAsync<PostPaymentResponse>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(paymentResponse);
        Assert.Equal(PaymentStatus.Declined, paymentResponse!.Status);
        Assert.Equal(int.Parse(cardNumber.ToString()[^4..]), paymentResponse.CardNumberLastFour);
        Assert.Equal(expiryMonth, paymentResponse.ExpiryMonth);
        Assert.Equal(expiryYear, paymentResponse.ExpiryYear);
        Assert.Equal(paymentRequest.Currency, paymentResponse.Currency);
        Assert.Equal(amount, paymentResponse.Amount);
    }

    [Theory, AutoData]
    public async Task InvalidPostParameters_ReturnsBadRequest(PostPaymentRequest paymentRequest)
    {
        // Arrange
        paymentRequest.CardNumber = "INVALIDCARDNUMBER";
        paymentRequest.ExpiryMonth = 13;
        paymentRequest.ExpiryYear = 2000;
        paymentRequest.Currency = "MONEY";
        paymentRequest.Cvv = -11;
        using StringContent jsonContent = new(
            JsonSerializer.Serialize(paymentRequest),
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await _sut.PostAsync("/api/Payments", jsonContent);
        var paymentResponse = await response.Content.ReadFromJsonAsync<PostPaymentResponse>();

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(paymentResponse);
    }

    [Fact]
    public async Task ValidGetRequest_ReturnsValidResponse()
    {
        // Arrange

        // Act
        var response = await _sut.GetAsync($"/api/Payments/{_storedGuid}");
        var paymentResponse = await response.Content.ReadFromJsonAsync<PostPaymentResponse>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(paymentResponse);
        Assert.Equal(_storedPayment.PaymentResponse.Id, paymentResponse!.Id);
        Assert.Equal(_storedPayment.PaymentResponse.Status, paymentResponse.Status);
        Assert.Equal(_storedPayment.PaymentResponse.CardNumberLastFour, paymentResponse.CardNumberLastFour);
        Assert.Equal(_storedPayment.PaymentResponse.ExpiryMonth, paymentResponse.ExpiryMonth);
        Assert.Equal(_storedPayment.PaymentResponse.ExpiryYear, paymentResponse.ExpiryYear);
        Assert.Equal(_storedPayment.PaymentResponse.Currency, paymentResponse.Currency);
        Assert.Equal(_storedPayment.PaymentResponse.Amount, paymentResponse.Amount);
    }

    [Fact]
    public async Task UnknownGetRequest_ReturnsNotFound()
    {
        // Arrange

        // Act
        var response = await _sut.GetAsync($"/api/Payments/{Guid.NewGuid()}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static int CreateInt(IFixture fixture, int min, int max)
    {
        return fixture.Create<int>() % (max - min + 1) + min;
    }
}