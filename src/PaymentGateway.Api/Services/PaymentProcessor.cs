using System.Diagnostics;
using System.Text;
using System.Text.Json;

using Microsoft.Extensions.Options;

using PaymentGateway.Api.Enums;
using PaymentGateway.Api.Models;
using PaymentGateway.Api.Models.Bank;
using PaymentGateway.Api.Models.Requests;
using PaymentGateway.Api.Models.Responses;
using PaymentGateway.Api.Options;

namespace PaymentGateway.Api.Services;

public interface IPaymentProcessor
{
    public Task<PostPaymentResponse> ProcessPayment(PostPaymentRequest request);
    public Task<PostPaymentResponse?> GetPayment(Guid paymentId);
}

public class PaymentProcessor(
    ActivitySource activitySource,
    HttpClient httpClient,
    IPaymentsRepository paymentsRepository,
    IOptions<BankOptions> bankOptions,
    ILogger<PaymentProcessor> logger) : IPaymentProcessor
{
    public async Task<PostPaymentResponse> ProcessPayment(PostPaymentRequest request)
    {
        activitySource.StartActivity();
        
        var bankRequest = new BankPostPaymentRequest
        {
            card_number = request.CardNumber,
            expiry_date = request.ExpiryMonth.ToString("D2") + "/" + request.ExpiryYear,
            Currency = request.Currency,
            Amount = request.Amount,
            Cvv = request.Cvv
        };

        var bankResponse = await SendPostPaymentRequest(bankRequest, request.ExpiryMonth, request.ExpiryYear);

        paymentsRepository.Add(bankResponse);

        var paymentResponse = bankResponse.PaymentResponse;

        return paymentResponse;
    }

    public async Task<PostPaymentResponse?> GetPayment(Guid paymentId)
    {
        activitySource.StartActivity();
        
        var response = paymentsRepository.Get(paymentId);

        if (response != null)
            return response.PaymentResponse;

        var bankResponse = await SendGetPaymentRequest(new BankGetPaymentRequest { Id = paymentId });
        if (bankResponse == null)
        {
            logger.LogError("Payment with {Id} not found in repository and bank request failed", paymentId);
            return null;
        }

        paymentsRepository.Add(bankResponse);
        
        return bankResponse.PaymentResponse;
    }

    private async Task<StoredPayment> SendPostPaymentRequest(BankPostPaymentRequest request, int expiryMonth,
        int expiryYear)
    {
        using StringContent jsonContent = new(
            JsonSerializer.Serialize(request),
            Encoding.UTF8,
            "application/json");

        BankPostPaymentResponse paymentResponse;
        var cardNumberLastFour = int.Parse(request.card_number[^4..]);
        try
        {
            using HttpResponseMessage response = await httpClient.PostAsync(
                bankOptions.Value.PaymentsEndpoint,
                jsonContent);

            response.EnsureSuccessStatusCode();

            paymentResponse = (await response.Content.ReadFromJsonAsync<BankPostPaymentResponse>())!;
            logger.LogInformation("Bank post payment authorized: {PaymentAuthorized}", paymentResponse.Authorized);
        }
        catch (HttpRequestException e)
        {
            logger.LogError("Bank post request failed: {Exception}", e.Message);
            return new StoredPayment
            {
                PaymentResponse = new PostPaymentResponse
                {
                    Id = Guid.NewGuid(),
                    Status = PaymentStatus.Declined,
                    CardNumberLastFour = cardNumberLastFour,
                    ExpiryMonth = expiryMonth,
                    ExpiryYear = expiryYear,
                    Currency = request.Currency,
                    Amount = request.Amount
                },
                AuthorizationCode = ""
            };
        }

        return new StoredPayment
        {
            PaymentResponse = new PostPaymentResponse
            {
                Id = Guid.NewGuid(),
                Status = paymentResponse.Authorized ? PaymentStatus.Authorized : PaymentStatus.Declined,
                CardNumberLastFour = cardNumberLastFour,
                ExpiryMonth = expiryMonth,
                ExpiryYear = expiryYear,
                Currency = request.Currency,
                Amount = request.Amount
            },
            AuthorizationCode = paymentResponse.authorization_code
        };
    }

    private async Task<StoredPayment?> SendGetPaymentRequest(BankGetPaymentRequest request)
    {
        var requestUri = $"{bankOptions.Value.PaymentsEndpoint}?id={request.Id}";

        BankGetPaymentResponse paymentResponse;
        try
        {
            using HttpResponseMessage response = await httpClient.GetAsync(requestUri);

            response.EnsureSuccessStatusCode();

            paymentResponse = (await response.Content.ReadFromJsonAsync<BankGetPaymentResponse>())!;
            logger.LogInformation("Bank get payment response: {PaymentResponse}", paymentResponse.Id);
        }
        catch (HttpRequestException e)
        {
            logger.LogError("Bank get request failed: {Exception}", e.Message);
            return null;
        }

        return new StoredPayment
        {
            PaymentResponse = new PostPaymentResponse
            {
                Id = Guid.NewGuid(),
                Status = paymentResponse.Status,
                CardNumberLastFour = paymentResponse.CardNumberLastFour,
                ExpiryMonth = paymentResponse.ExpiryMonth,
                ExpiryYear = paymentResponse.ExpiryYear,
                Currency = paymentResponse.Currency,
                Amount = paymentResponse.Amount
            },
            AuthorizationCode = paymentResponse.authorization_code
        };
    }
}