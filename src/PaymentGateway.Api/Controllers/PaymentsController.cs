using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

using PaymentGateway.Api.Extensions;
using PaymentGateway.Api.Models.Requests;
using PaymentGateway.Api.Models.Responses;
using PaymentGateway.Api.Options;
using PaymentGateway.Api.Services;

namespace PaymentGateway.Api.Controllers;

[Route("api/[controller]")]
[ApiController]
public class PaymentsController(
    ActivitySource activitySource,
    IPaymentProcessor paymentProcessor,
    IOptions<CurrencyCodes> currencyCodes,
    ILogger<PaymentsController> logger) : Controller
{
    [HttpPost("")]
    public async Task<ActionResult<PostPaymentResponse?>> PostPaymentAsync(PostPaymentRequest request)
    {
        using var activity = activitySource.StartActivity();
        activity?.AddTag("http.request.method", "POST");
        activity?.AddTag("card.last_four", request.CardNumber.ToCardNumberLastFour());

        try
        {
            if (!currencyCodes.Value.Codes.Contains(request.Currency))
            {
                activity?.AddTag("http.response.status_code", 400);
                activity?.AddTag("error", true);
                return BadRequest("Unsupported currency");
            }

            logger.LogInformation(
                "Received post request with card number ending in {CardNumberLastFour}, expiry month {ExpiryMonth}," +
                " expiry year {ExpiryYear}, currency {Currency} and amount {Amount}",
                request.CardNumber.ToCardNumberLastFour(), request.ExpiryMonth, request.ExpiryYear, request.Currency,
                request.Amount);

            var headerKey = Request.HttpContext.Request.Headers["UniqueKey"].FirstOrDefault();
            Guid requestKey = string.IsNullOrEmpty(headerKey) ? CreateHashFromRequest(request) : Guid.Parse(headerKey);
            var existingPayment = await paymentProcessor.GetPayment(requestKey);
            if (existingPayment != null)
            {
                activity?.AddTag("http.response.status_code", 200);
                activity?.AddTag("repeated.request", true);
                return new OkObjectResult(existingPayment);
            }

            var paymentRequest = new PaymentRequest
            {
                Id = requestKey,
                CardNumber = request.CardNumber,
                ExpiryMonth = request.ExpiryMonth,
                ExpiryYear = request.ExpiryYear,
                Currency = request.Currency,
                Amount = request.Amount,
                Cvv = request.Cvv
            };

            var payment = await paymentProcessor.ProcessPayment(paymentRequest);

            activity?.AddTag("payment.id", payment.Id);
            activity?.AddTag("payment.status", payment.Status.ToString());
            activity?.AddTag("http.response.status_code", 200);

            return new OkObjectResult(payment);
        }
        catch (Exception ex)
        {
            activity?.AddTag("error", true);
            activity?.AddTag("error.message", ex.Message);
            throw;
        }
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PostPaymentResponse?>> GetPaymentAsync(Guid id)
    {
        using var activity = activitySource.StartActivity();
        activity?.AddTag("http.request.method", "GET");
        activity?.AddTag("payment.id", id);

        try
        {
            logger.LogInformation("Received get request for payment with id {PaymentId}", id);

            var payment = await paymentProcessor.GetPayment(id);

            if (payment == null)
            {
                activity?.AddTag("http.response.status_code", 404);
                activity?.AddTag("error", true);
                return new NotFoundResult();
            }

            activity?.AddTag("payment.status", payment.Status.ToString());
            activity?.AddTag("http.response.status_code", 200);

            return new OkObjectResult(payment);
        }
        catch (Exception ex)
        {
            activity?.AddTag("error", true);
            activity?.AddTag("error.message", ex.Message);
            throw;
        }
    }

    private static Guid CreateHashFromRequest(PostPaymentRequest request)
    {
        byte[] hash = MD5.HashData(Encoding.UTF8.GetBytes(request.CardNumber + request.Amount + request.Currency));
        return new Guid(hash);
    }
}