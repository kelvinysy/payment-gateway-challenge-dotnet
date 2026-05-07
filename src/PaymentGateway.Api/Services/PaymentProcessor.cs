using System.Diagnostics;
using System.Text;
using System.Text.Json;

using Microsoft.Extensions.Options;

using PaymentGateway.Api.Enums;
using PaymentGateway.Api.Extensions;
using PaymentGateway.Api.Models;
using PaymentGateway.Api.Models.Bank;
using PaymentGateway.Api.Models.Requests;
using PaymentGateway.Api.Models.Responses;
using PaymentGateway.Api.Options;

namespace PaymentGateway.Api.Services;

public interface IPaymentProcessor
{
    public Task<PostPaymentResponse> ProcessPayment(PaymentRequest request);
    public Task<PostPaymentResponse?> GetPayment(Guid paymentId);
}

public class PaymentProcessor(
    ActivitySource activitySource,
    HttpClient httpClient,
    IPaymentsRepository paymentsRepository,
    IOptions<BankOptions> bankOptions,
    ILogger<PaymentProcessor> logger) : IPaymentProcessor
{
    public async Task<PostPaymentResponse> ProcessPayment(PaymentRequest request)
    {
        using var activity = activitySource.StartActivity();
        activity?.AddTag("payment.id", request.Id);
        
        try
        {
            var bankRequest = new BankPostPaymentRequest
            {
                card_number = request.CardNumber,
                expiry_date = request.ExpiryMonth.ToString("D2") + "/" + request.ExpiryYear,
                Currency = request.Currency,
                Amount = request.Amount,
                Cvv = request.Cvv
            };

            var bankResponse =
                await SendPostPaymentRequest(bankRequest, request.ExpiryMonth, request.ExpiryYear, request.Id);

            var storedPayment = paymentsRepository.Add(bankResponse);
            
            activity?.AddTag("payment.status", storedPayment.PaymentResponse.Status.ToString());

            return storedPayment.PaymentResponse;
        }
        catch (Exception ex)
        {
            activity?.AddTag("error", true);
            activity?.AddTag("error.message", ex.Message);
            throw;
        }
    }

    public async Task<PostPaymentResponse?> GetPayment(Guid paymentId)
    {
        using var activity = activitySource.StartActivity();
        activity?.AddTag("payment.id", paymentId);
        
        try
        {
            var response = paymentsRepository.Get(paymentId);

            if (response != null)
            {
                activity?.AddTag("cache.hit", true);
                return response.PaymentResponse;
            }

            activity?.AddTag("cache.hit", false);
            
            var bankResponse = await SendGetPaymentRequest(new BankGetPaymentRequest { Id = paymentId });
            if (bankResponse == null)
            {
                activity?.AddTag("error", true);
                activity?.AddTag("error.message", "Payment not found in repository or bank");
                logger.LogError("Payment with {Id} not found in repository and bank request failed", paymentId);
                return null;
            }

            var storedPayment = paymentsRepository.Add(bankResponse);
            
            return storedPayment.PaymentResponse;
        }
        catch (Exception ex)
        {
            activity?.AddTag("error", true);
            activity?.AddTag("error.message", ex.Message);
            throw;
        }
    }

    private async Task<StoredPayment> SendPostPaymentRequest(BankPostPaymentRequest request, int expiryMonth,
        int expiryYear, Guid paymentId)
    {
        using var activity = activitySource.StartActivity();
        
        using StringContent jsonContent = new(
            JsonSerializer.Serialize(request),
            Encoding.UTF8,
            "application/json");

        BankPostPaymentResponse paymentResponse;
        var cardNumberLastFour = int.Parse(request.card_number.ToCardNumberLastFour());
        try
        {
            using HttpResponseMessage response = await httpClient.PostAsync(
                bankOptions.Value.PaymentsEndpoint,
                jsonContent);

            response.EnsureSuccessStatusCode();

            paymentResponse = (await response.Content.ReadFromJsonAsync<BankPostPaymentResponse>())!;
            activity?.AddTag("http.response.status_code", (int)response.StatusCode);
            activity?.AddTag("bank.response.authorized", paymentResponse.Authorized);
            logger.LogInformation("Bank post payment authorized: {PaymentAuthorized}", paymentResponse.Authorized);
        }
        catch (HttpRequestException e)
        {
            activity?.AddTag("error", true);
            activity?.AddTag("error.message", "Bank POST request failed");
            logger.LogError("Bank post request failed: {Exception}", e.Message);
            return new StoredPayment
            {
                PaymentResponse = new PostPaymentResponse
                {
                    Id = paymentId,
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
                Id = paymentId,
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
        using var activity = activitySource.StartActivity();
        activity?.AddTag("payment.id", request.Id);
        
        var requestUri = $"{bankOptions.Value.PaymentsEndpoint}?id={request.Id}";

        BankGetPaymentResponse paymentResponse;
        try
        {
            using HttpResponseMessage response = await httpClient.GetAsync(requestUri);

            response.EnsureSuccessStatusCode();

            paymentResponse = (await response.Content.ReadFromJsonAsync<BankGetPaymentResponse>())!;
            activity?.AddTag("http.response.status_code", (int)response.StatusCode);
            activity?.AddTag("payment.status", paymentResponse.Status.ToString());
            logger.LogInformation("Bank get payment response: {PaymentResponse}", paymentResponse.Id);
        }
        catch (HttpRequestException e)
        {
            activity?.AddTag("error", true);
            activity?.AddTag("error.message", "Bank GET request failed");
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