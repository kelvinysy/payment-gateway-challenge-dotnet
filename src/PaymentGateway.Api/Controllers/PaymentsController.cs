using System.Diagnostics;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

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
        activitySource.StartActivity();
        
        if (!currencyCodes.Value.Codes.Contains(request.Currency))
            return BadRequest("Unsupported currency");
        
        logger.LogInformation(
            "Received post request with card number ending in {CardNumberLastFour}, expiry month {ExpiryMonth}," +
            " expiry year {ExpiryYear}, currency {Currency} and amount {Amount}",
            request.CardNumber[^4..], request.ExpiryMonth, request.ExpiryYear, request.Currency, request.Amount);

        var payment = await paymentProcessor.ProcessPayment(request);

        return new OkObjectResult(payment);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PostPaymentResponse?>> GetPaymentAsync(Guid id)
    {
        activitySource.StartActivity();
        
        logger.LogInformation("Received get request for payment with id {PaymentId}", id);

        var payment = await paymentProcessor.GetPayment(id);

        if (payment == null)
            return new NotFoundResult();

        return new OkObjectResult(payment);
    }
}